import { createHash } from "node:crypto";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { config as loadEnv } from "dotenv";
import sql from "mssql";
import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  splitSqlBatches,
  validateMigrationSql,
  validateReadOnlySql,
} from "./sqlSafety.js";

const toolRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
loadEnv({ path: resolve(toolRoot, ".env"), quiet: true });

const EXPECTED_DATABASE = "WiseLinePortal_Staging";

function requiredEnv(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`Required environment variable ${name} is not set.`);
  return value;
}

function booleanEnv(name: string, fallback: boolean): boolean {
  const value = process.env[name]?.trim().toLowerCase();
  if (!value) return fallback;
  if (value === "true") return true;
  if (value === "false") return false;
  throw new Error(`${name} must be true or false.`);
}

function numberEnv(name: string, fallback: number): number {
  const raw = process.env[name]?.trim();
  if (!raw) return fallback;
  const value = Number(raw);
  if (!Number.isInteger(value) || value <= 0) {
    throw new Error(`${name} must be a positive integer.`);
  }
  return value;
}

function textResult(value: unknown) {
  return {
    content: [{ type: "text" as const, text: JSON.stringify(value, null, 2) }],
  };
}

const database = process.env.WISELINE_PORTAL_SQL_DATABASE?.trim() || EXPECTED_DATABASE;
if (database.toLowerCase() !== EXPECTED_DATABASE.toLowerCase()) {
  throw new Error(
    `This MCP server is locked to ${EXPECTED_DATABASE}; configured database was ${database}.`
  );
}

const pool = new sql.ConnectionPool({
  server: requiredEnv("WISELINE_PORTAL_SQL_HOST"),
  port: numberEnv("WISELINE_PORTAL_SQL_PORT", 1433),
  database,
  user: requiredEnv("WISELINE_PORTAL_SQL_USER"),
  password: requiredEnv("WISELINE_PORTAL_SQL_PASSWORD"),
  options: {
    encrypt: booleanEnv("WISELINE_PORTAL_SQL_ENCRYPT", true),
    trustServerCertificate: booleanEnv("WISELINE_PORTAL_SQL_TRUST_CERT", false),
    enableArithAbort: true,
  },
  pool: {
    max: 4,
    min: 0,
    idleTimeoutMillis: 30_000,
  },
  requestTimeout: numberEnv("WISELINE_PORTAL_SQL_REQUEST_TIMEOUT_MS", 30_000),
});

try {
  await pool.connect();
  const identity = await pool.request().query(`
    SELECT
      DB_NAME() AS database_name,
      ORIGINAL_LOGIN() AS login_name,
      USER_NAME() AS database_user,
      IS_ROLEMEMBER(N'db_owner') AS is_db_owner;
  `);
  const row = identity.recordset[0] as
    | { database_name?: string; is_db_owner?: number }
    | undefined;
  if (row?.database_name?.toLowerCase() !== EXPECTED_DATABASE.toLowerCase()) {
    throw new Error(`Connected to unexpected database ${row?.database_name ?? "unknown"}.`);
  }
  if (row.is_db_owner !== 1) {
    throw new Error("The configured database user is not a member of db_owner.");
  }
} catch (error) {
  console.error(
    "mcp-wiselineportal-db: database connection failed:",
    error instanceof Error ? error.message : error
  );
  await pool.close().catch(() => undefined);
  process.exit(1);
}

const server = new McpServer({
  name: "mcp-wiselineportal-db",
  version: "1.0.0",
});

server.tool("test_connection", "Verify the fixed database, login, database user, and db_owner membership.", {}, async () => {
  const result = await pool.request().query(`
    SELECT
      DB_NAME() AS database_name,
      ORIGINAL_LOGIN() AS login_name,
      USER_NAME() AS database_user,
      IS_ROLEMEMBER(N'db_owner') AS is_db_owner,
      SYSUTCDATETIME() AS checked_at_utc;
  `);
  return textResult(result.recordset[0]);
});

server.tool("list_schemas", "List non-system schemas in WiseLinePortal_Staging.", {}, async () => {
  const result = await pool.request().query(`
    SELECT s.name AS schema_name
    FROM sys.schemas AS s
    WHERE s.name NOT IN (N'dbo', N'guest', N'sys', N'INFORMATION_SCHEMA')
      AND s.principal_id <> 16384
    ORDER BY s.name;
  `);
  return textResult(result.recordset);
});

server.tool("list_tables", "List tables and approximate row counts in WiseLinePortal_Staging.", {}, async () => {
  const result = await pool.request().query(`
    SELECT
      s.name AS schema_name,
      t.name AS table_name,
      SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS approximate_rows
    FROM sys.tables AS t
    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    LEFT JOIN sys.partitions AS p ON p.object_id = t.object_id
    GROUP BY s.name, t.name
    ORDER BY s.name, t.name;
  `);
  return textResult(result.recordset);
});

server.tool(
  "describe_table",
  "Describe columns, defaults, identity, computed columns, and primary-key membership for one table.",
  { schema: z.string().min(1).max(128), table: z.string().min(1).max(128) },
  async ({ schema, table }) => {
    const result = await pool.request()
      .input("schema", sql.NVarChar(128), schema)
      .input("table", sql.NVarChar(128), table)
      .query(`
        SELECT
          c.column_id,
          c.name AS column_name,
          ty.name AS data_type,
          c.max_length,
          c.precision,
          c.scale,
          c.is_nullable,
          c.is_identity,
          c.is_computed,
          dc.definition AS default_definition,
          CAST(CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS bit) AS is_primary_key
        FROM sys.tables AS t
        JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        JOIN sys.columns AS c ON c.object_id = t.object_id
        JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN sys.default_constraints AS dc ON dc.object_id = c.default_object_id
        LEFT JOIN (
          SELECT ic.object_id, ic.column_id
          FROM sys.indexes AS i
          JOIN sys.index_columns AS ic
            ON ic.object_id = i.object_id AND ic.index_id = i.index_id
          WHERE i.is_primary_key = 1
        ) AS pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
        WHERE s.name = @schema AND t.name = @table
        ORDER BY c.column_id;
      `);
    return textResult(result.recordset);
  }
);

server.tool("list_database_objects", "List portal tables, views, procedures, and functions.", {}, async () => {
  const result = await pool.request().query(`
    SELECT
      s.name AS schema_name,
      o.name AS object_name,
      o.type_desc,
      o.create_date,
      o.modify_date
    FROM sys.objects AS o
    JOIN sys.schemas AS s ON s.schema_id = o.schema_id
    WHERE o.is_ms_shipped = 0
      AND o.type IN (N'U', N'V', N'P', N'FN', N'IF', N'TF')
    ORDER BY s.name, o.type_desc, o.name;
  `);
  return textResult(result.recordset);
});

server.tool(
  "get_object_definition",
  "Return the SQL definition of a view, stored procedure, or function.",
  { schema: z.string().min(1).max(128), object: z.string().min(1).max(128) },
  async ({ schema, object }) => {
    const result = await pool.request()
      .input("schema", sql.NVarChar(128), schema)
      .input("object", sql.NVarChar(128), object)
      .query(`
        SELECT
          s.name AS schema_name,
          o.name AS object_name,
          o.type_desc,
          sm.definition
        FROM sys.objects AS o
        JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        JOIN sys.sql_modules AS sm ON sm.object_id = o.object_id
        WHERE s.name = @schema AND o.name = @object;
      `);
    if (result.recordset.length === 0) {
      throw new Error(`No programmable object found at ${schema}.${object}.`);
    }
    return textResult(result.recordset[0]);
  }
);

server.tool(
  "run_readonly_query",
  "Run a SELECT or CTE query against WiseLinePortal_Staging. Results are capped at 200 rows.",
  { query: z.string().min(1).max(100_000) },
  async ({ query }) => {
    validateReadOnlySql(query);
    const MAX_ROWS = 200;
    const request = pool.request();
    request.stream = true;
    const rows: Record<string, unknown>[] = [];
    let truncated = false;

    await new Promise<void>((resolvePromise, rejectPromise) => {
      request.on("row", (row: Record<string, unknown>) => {
        if (rows.length < MAX_ROWS) rows.push(row);
        else if (!truncated) {
          truncated = true;
          request.cancel();
        }
      });
      request.on("error", (error: Error & { code?: string }) => {
        if (error.code !== "ECANCEL") rejectPromise(error);
      });
      request.on("done", () => resolvePromise());
      request.query(query);
    });

    return textResult({ rows, truncated, maximum_rows: MAX_ROWS });
  }
);

server.tool("list_migrations", "List migrations applied through this MCP server.", {}, async () => {
  const result = await pool.request().query(`
    IF OBJECT_ID(N'dbo.__PortalMcpMigrations', N'U') IS NULL
      SELECT
        CAST(NULL AS nvarchar(128)) AS migration_id,
        CAST(NULL AS nvarchar(64)) AS checksum_sha256,
        CAST(NULL AS nvarchar(400)) AS description,
        CAST(NULL AS datetime2) AS applied_at_utc,
        CAST(NULL AS sysname) AS applied_by
      WHERE 1 = 0;
    ELSE
      SELECT migration_id, checksum_sha256, description, applied_at_utc, applied_by
      FROM dbo.__PortalMcpMigrations
      ORDER BY applied_at_utc, migration_id;
  `);
  return textResult(result.recordset);
});

server.tool(
  "apply_migration",
  "Apply one reviewed, idempotent schema migration to WiseLinePortal_Staging inside a transaction and record its SHA-256 checksum.",
  {
    migration_id: z.string().regex(/^\d{8}_\d{3}_[a-z0-9_]+$/).max(128),
    description: z.string().min(1).max(400),
    sql_text: z.string().min(1).max(1_000_000),
  },
  async ({ migration_id, description, sql_text }) => {
    validateMigrationSql(sql_text);
    const batches = splitSqlBatches(sql_text);
    if (batches.length === 0) throw new Error("Migration contains no executable batches.");
    const checksum = createHash("sha256").update(sql_text, "utf8").digest("hex");
    const transaction = new sql.Transaction(pool);

    try {
      await transaction.begin(sql.ISOLATION_LEVEL.SERIALIZABLE);
      const lockResult = await new sql.Request(transaction).query(`
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock
          @Resource = N'WiseLinePortal.SchemaMigration',
          @LockMode = N'Exclusive',
          @LockOwner = N'Transaction',
          @LockTimeout = 30000;
        IF @result < 0 THROW 51000, 'Could not acquire the portal migration lock.', 1;
      `);
      void lockResult;

      await new sql.Request(transaction).query(`
        IF OBJECT_ID(N'dbo.__PortalMcpMigrations', N'U') IS NULL
        BEGIN
          CREATE TABLE dbo.__PortalMcpMigrations
          (
            migration_id nvarchar(128) NOT NULL
              CONSTRAINT PK___PortalMcpMigrations PRIMARY KEY,
            checksum_sha256 char(64) NOT NULL,
            description nvarchar(400) NOT NULL,
            applied_at_utc datetime2(3) NOT NULL
              CONSTRAINT DF___PortalMcpMigrations_AppliedAt DEFAULT SYSUTCDATETIME(),
            applied_by sysname NOT NULL
              CONSTRAINT DF___PortalMcpMigrations_AppliedBy DEFAULT ORIGINAL_LOGIN()
          );
        END;
      `);

      const existing = await new sql.Request(transaction)
        .input("migration_id", sql.NVarChar(128), migration_id)
        .query(`
          SELECT checksum_sha256, applied_at_utc, applied_by
          FROM dbo.__PortalMcpMigrations
          WHERE migration_id = @migration_id;
        `);

      if (existing.recordset.length > 0) {
        const row = existing.recordset[0] as { checksum_sha256: string };
        if (row.checksum_sha256 !== checksum) {
          throw new Error(`Migration ${migration_id} already exists with a different checksum.`);
        }
        await transaction.commit();
        return textResult({ status: "already_applied", migration_id, checksum_sha256: checksum });
      }

      for (const batch of batches) {
        await new sql.Request(transaction).batch(batch);
      }

      await new sql.Request(transaction)
        .input("migration_id", sql.NVarChar(128), migration_id)
        .input("checksum", sql.Char(64), checksum)
        .input("description", sql.NVarChar(400), description)
        .query(`
          INSERT dbo.__PortalMcpMigrations
            (migration_id, checksum_sha256, description)
          VALUES
            (@migration_id, @checksum, @description);
        `);

      await transaction.commit();
      return textResult({
        status: "applied",
        migration_id,
        checksum_sha256: checksum,
        batch_count: batches.length,
      });
    } catch (error) {
      await transaction.rollback().catch(() => undefined);
      throw error;
    }
  }
);

const shutdown = async () => {
  await pool.close().catch(() => undefined);
  process.exit(0);
};
process.once("SIGINT", shutdown);
process.once("SIGTERM", shutdown);

const transport = new StdioServerTransport();
await server.connect(transport);
