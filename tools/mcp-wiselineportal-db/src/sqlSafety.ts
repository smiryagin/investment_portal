const STRING_OR_COMMENT = /'(?:[^']|'')*'|--[^\r\n]*|\/\*[\s\S]*?\*\//g;

export function scrubSql(sqlText: string): string {
  return sqlText.replace(STRING_OR_COMMENT, (value) =>
    value.startsWith("'") ? "''" : " "
  );
}

const MIGRATION_BLOCKS: Array<{ pattern: RegExp; reason: string }> = [
  { pattern: /(^|\s|;)use\s+/i, reason: "USE statements are not allowed" },
  { pattern: /\b(create|alter|drop)\s+(login|user|role|server\s+role|credential|endpoint)\b/i, reason: "security principal or endpoint changes are not allowed" },
  { pattern: /\b(grant|deny|revoke)\b/i, reason: "permission changes are not allowed" },
  { pattern: /\balter\s+(authorization\s+on\s+database|database)\b/i, reason: "database ownership or configuration changes are not allowed" },
  { pattern: /\b(backup|restore|shutdown|reconfigure)\b/i, reason: "server operations are not allowed" },
  { pattern: /\b(sp_configure|xp_cmdshell|sp_addlinkedserver|sp_serveroption)\b/i, reason: "server configuration procedures are not allowed" },
  { pattern: /\bexecute\s+as\s+login\b/i, reason: "server impersonation is not allowed" },
  { pattern: /\b(openrowset|opendatasource|openquery|bulk\s+insert)\b/i, reason: "external data access is not allowed" },
  { pattern: /\b(create|alter|drop)\s+(external\s+data\s+source|assembly)\b/i, reason: "external data sources and assemblies are not allowed" },
  { pattern: /(^|\s)(begin\s+tran(?:saction)?|commit(?:\s+tran(?:saction)?)?|rollback(?:\s+tran(?:saction)?)?)(\s|;|$)/i, reason: "transaction control is managed by the MCP server" },
  { pattern: /(^|\r?\n)\s*(:r|:setvar|!!)/im, reason: "SQLCMD and shell directives are not allowed" },
  { pattern: /(?:\[?[a-z_][\w$#@]*\]?\s*\.){2}\s*\[?[a-z_][\w$#@]*\]?/i, reason: "three-part or four-part object names are not allowed" },
];

export function validateMigrationSql(sqlText: string): void {
  if (sqlText.trim().length === 0) {
    throw new Error("Migration SQL cannot be empty.");
  }

  const scrubbed = scrubSql(sqlText);
  for (const block of MIGRATION_BLOCKS) {
    if (block.pattern.test(scrubbed)) {
      throw new Error(`Migration rejected: ${block.reason}.`);
    }
  }
}

export function validateReadOnlySql(sqlText: string): void {
  const normalized = scrubSql(sqlText).trim();
  if (!/^(select|with)\b/i.test(normalized)) {
    throw new Error("Only SELECT queries or CTEs ending in SELECT are allowed.");
  }

  const forbidden = /\b(insert|update|delete|drop|alter|create|truncate|merge|exec|execute|grant|deny|revoke|into|waitfor|openrowset|opendatasource|openquery)\b/i;
  if (forbidden.test(normalized)) {
    throw new Error("The query contains a write, DDL, execution, or external-access command.");
  }
}

export function splitSqlBatches(sqlText: string): string[] {
  const batches: string[] = [];
  const lines = sqlText.replace(/\r\n/g, "\n").split("\n");
  let current: string[] = [];

  for (const line of lines) {
    if (/^\s*go\s*;?\s*$/i.test(line)) {
      const batch = current.join("\n").trim();
      if (batch.length > 0) batches.push(batch);
      current = [];
      continue;
    }
    current.push(line);
  }

  const finalBatch = current.join("\n").trim();
  if (finalBatch.length > 0) batches.push(finalBatch);
  return batches;
}
