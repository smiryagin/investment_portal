# Database identities and permissions

Use separate SQL identities for deployment, portal runtime, and Trade integration. Do not reuse `WiseLinePortal_Deploy` in IIS. Staging currently reuses the deployment and runtime server logins but maps them independently inside `WiseLinePortal_Staging`; split these identities before production launch.

## Portal runtime identity

Run the following as a SQL Server administrator after the initial migration has created the schemas. Replace the password outside source control.

```sql
USE [master];
GO
CREATE LOGIN [WiseLinePortal_Runtime]
WITH PASSWORD = N'<strong-random-password>',
     CHECK_POLICY = ON,
     CHECK_EXPIRATION = OFF,
     DEFAULT_DATABASE = [WiseLinePortal];
GO

USE [WiseLinePortal];
GO
CREATE USER [WiseLinePortal_Runtime]
FOR LOGIN [WiseLinePortal_Runtime]
WITH DEFAULT_SCHEMA = [portal];
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[auth] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[portal] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[billing] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[integration] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT ON SCHEMA::[audit] TO [WiseLinePortal_Runtime];
GO
```

The runtime login receives data access only. It does not receive `ALTER`, `CONTROL`, `CREATE TABLE`, role membership, or access to the `deployment` schema.

For the current staging environment, map the existing server login into the
staging database after the schema migration:

```sql
USE [WiseLinePortal_Staging];
GO
IF USER_ID(N'WiseLinePortal_Runtime') IS NULL
BEGIN
    CREATE USER [WiseLinePortal_Runtime]
    FOR LOGIN [WiseLinePortal_Runtime]
    WITH DEFAULT_SCHEMA = [portal];
END;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[auth] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[portal] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[billing] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[integration] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT ON SCHEMA::[audit] TO [WiseLinePortal_Runtime];
GO
```

## Migration identity

`WiseLinePortal_Deploy` is currently the database owner used by both environment migration bundles and the production backup step. Store its connection only as `PORTAL_MIGRATION_DATABASE_CONNECTION_STRING` in each protected GitHub environment. Rotate or disable it when deployment access is not needed. Introduce separate staging and production deployment identities before production launch.

## Trade connector

Apply `investment_mcp/sql/012_add_portal_integration.sql`, create `InvestmentPortal_Connector` in `Trade`, and add it only to the `investment_portal_runtime` role as shown in `TRADE_DATABASE_CONTRACT.md`. The role grants the seven portal contract procedures and explicitly denies direct reads or writes to the `invest` schema. Do not add the connector to `db_datareader`, `db_datawriter`, or `db_owner`.

Use encrypted SQL connections. Staging may temporarily trust the current server certificate; production should use a certificate trusted by the portal server and set `TrustServerCertificate=False`.
