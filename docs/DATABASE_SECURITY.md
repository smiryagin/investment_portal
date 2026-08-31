# Database identities and permissions

Use separate SQL identities for deployment, portal runtime, and Trade integration. Do not reuse `WiseLinePortal_Deploy` in IIS.

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
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[portal] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[billing] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[audit] TO [WiseLinePortal_Runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[integration] TO [WiseLinePortal_Runtime];
GO
```

The runtime login receives data access only. It does not receive `ALTER`, `CONTROL`, `CREATE TABLE`, role membership, or access to the `deployment` schema. Create an equivalent `WiseLinePortal_Staging_Runtime` login/user in `WiseLinePortal_Staging`.

## Migration identity

`WiseLinePortal_Deploy` is the database owner used by the migration bundle and production backup step. Store its connection only as `PORTAL_MIGRATION_DATABASE_CONNECTION_STRING` in the protected GitHub environment. Rotate or disable it when deployment access is not needed.

Create a separate staging deployment identity limited to `WiseLinePortal_Staging`.

## Trade connector

Create `InvestmentPortal_Connector` in `Trade`, then grant only the five procedure permissions listed in `TRADE_DATABASE_CONTRACT.md`. Do not add it to `db_datareader`, `db_datawriter`, or `db_owner`.

Use encrypted SQL connections. Staging may temporarily trust the current server certificate; production should use a certificate trusted by the portal server and set `TrustServerCertificate=False`.
