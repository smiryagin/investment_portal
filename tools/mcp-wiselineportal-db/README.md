# WiseLinePortal database development MCP

Local TypeScript MCP server for developing the `WiseLinePortal` SQL Server database. It uses stdio transport and is intended only for local Codex development—not production runtime access.

## Safety model

- The configured database must be exactly `WiseLinePortal`.
- Startup fails unless the database identity is a member of `db_owner`.
- Read tools inspect schemas, tables, programmable objects, and capped query results.
- All writes go through `apply_migration`, which validates the script, obtains an application lock, runs every batch in one transaction, and records an immutable SHA-256 checksum.
- Database switching, cross-database object names, login/user/role changes, permission changes, server configuration, external data access, and SQLCMD directives are blocked.
- SQL Server permissions remain the ultimate security boundary. The `WiseLinePortal_Deploy` login must have access only to `WiseLinePortal` and must never be a server administrator.

## Tools

- `test_connection`
- `list_schemas`
- `list_tables`
- `describe_table`
- `list_database_objects`
- `get_object_definition`
- `run_readonly_query`
- `list_migrations`
- `apply_migration`

## One-time setup

### 1. Install and build

```powershell
cd C:\Users\AndreySmiryagin\source\repos\investment_portal\tools\mcp-wiselineportal-db
npm install
npm run test
npm run build
```

### 2. Configure non-secret settings

Copy `.env.example` to `.env` and update the SQL host and TLS settings. Never put the password in `.env`.

For SQL Server on the same computer, start with:

```dotenv
WISELINE_PORTAL_SQL_HOST=localhost
WISELINE_PORTAL_SQL_PORT=1433
WISELINE_PORTAL_SQL_DATABASE=WiseLinePortal
WISELINE_PORTAL_SQL_USER=WiseLinePortal_Deploy
WISELINE_PORTAL_SQL_ENCRYPT=true
WISELINE_PORTAL_SQL_TRUST_CERT=false
WISELINE_PORTAL_SQL_REQUEST_TIMEOUT_MS=30000
```

If the development SQL Server uses a locally issued certificate, set `WISELINE_PORTAL_SQL_TRUST_CERT=true` only for this local development connection.

### 3. Store the password outside Git

Set it as a Windows **User** environment variable from PowerShell. Do not paste it into source files or Codex chat.

```powershell
[Environment]::SetEnvironmentVariable(
  'WISELINE_PORTAL_SQL_PASSWORD',
  '<your WiseLinePortal_Deploy password>',
  'User'
)
```

Close and reopen Codex after setting the variable so the desktop process inherits it.

### 4. Register the local MCP server with Codex

```powershell
codex mcp add wiseline-portal-db -- `
  node C:\Users\AndreySmiryagin\source\repos\investment_portal\tools\mcp-wiselineportal-db\dist\server.js
```

Verify registration:

```powershell
codex mcp get wiseline-portal-db
codex mcp list
```

Restart Codex or open a new task after registration. Existing running tasks do not gain newly registered MCP tools.

## Credential rotation

After changing the SQL login password, update the Windows User environment variable and restart Codex. Before production launch, disable or rotate the deployment login when it is not needed and configure the web API with a separate least-privilege runtime identity.

## Development workflow

1. Inspect the current schema.
2. Draft a numbered migration such as `20260828_001_initial_portal_schema`.
3. Review and approve the SQL.
4. Apply it through `apply_migration`.
5. Reinspect affected objects and record verification results.

Never use this MCP server from the production web application.
