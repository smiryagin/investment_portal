import assert from "node:assert/strict";
import test from "node:test";
import {
  splitSqlBatches,
  validateMigrationSql,
  validateReadOnlySql,
} from "../src/sqlSafety.js";

test("splits SQL batches on standalone GO lines", () => {
  assert.deepEqual(
    splitSqlBatches("CREATE TABLE portal.A(Id int);\nGO\nCREATE VIEW portal.V AS SELECT Id FROM portal.A;"),
    ["CREATE TABLE portal.A(Id int);", "CREATE VIEW portal.V AS SELECT Id FROM portal.A;"]
  );
});

test("allows ordinary schema migration SQL", () => {
  assert.doesNotThrow(() =>
    validateMigrationSql(`
      CREATE SCHEMA portal;
      GO
      CREATE TABLE portal.Users (UserId uniqueidentifier NOT NULL PRIMARY KEY);
      GO
      CREATE OR ALTER PROCEDURE portal.GetUsers AS SELECT UserId FROM portal.Users;
    `)
  );
});

test("rejects database switching and security administration", () => {
  assert.throws(() => validateMigrationSql("USE Trade; SELECT 1;"), /USE statements/);
  assert.throws(() => validateMigrationSql("CREATE LOGIN Bad WITH PASSWORD='x';"), /security principal/);
  assert.throws(() => validateMigrationSql("GRANT CONTROL SERVER TO Bad;"), /permission changes/);
});

test("rejects cross-database names and transaction control", () => {
  assert.throws(() => validateMigrationSql("SELECT * FROM Trade.dbo.Account;"), /three-part/);
  assert.throws(() => validateMigrationSql("BEGIN TRAN; CREATE TABLE dbo.T(Id int); COMMIT;"), /transaction control/);
});

test("ignores blocked words inside comments and string literals", () => {
  assert.doesNotThrow(() =>
    validateMigrationSql("CREATE TABLE dbo.Note(Value nvarchar(100) DEFAULT 'USE Trade'); -- GRANT")
  );
});

test("allows SELECT and rejects read query side effects", () => {
  assert.doesNotThrow(() => validateReadOnlySql("WITH x AS (SELECT 1 AS n) SELECT n FROM x"));
  assert.throws(() => validateReadOnlySql("SELECT * INTO dbo.Copy FROM dbo.Source"), /write, DDL/);
  assert.throws(() => validateReadOnlySql("EXEC dbo.DoSomething"), /Only SELECT/);
});
