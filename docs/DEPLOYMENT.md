# GitHub Actions and IIS deployment

## Branch flow

- Create feature branches and open pull requests into `staging`.
- Protect `staging` and `main`; require the `CI / validate` check and disable direct pushes.
- A push/merge to `staging` builds once and deploys to the staging IIS site.
- Promote with a pull request from `staging` to `main`.
- A push/merge to `main` builds once, backs up and migrates production, then deploys the production IIS site.
- Configure required reviewers on the GitHub `production` environment.

Only GitHub-hosted runners build or test repository code. The deployment job runs the already-built artifact on the dedicated self-hosted Windows runner labeled `wiseline-portal-deploy`.

## Server prerequisites

- .NET 10 Hosting Bundle
- IIS with the ASP.NET Core Module
- PowerShell WebAdministration module
- `SqlServer` PowerShell module on the deployment runner
- A low-privilege GitHub Actions runner service account with access only to the two portal sites, app pools, release/config directories, and the migration/backup operations

Recommended IIS resources:

| Environment | Host name                   | Site                     | App pool                 | Database                 |
| ----------- | --------------------------- | ------------------------ | ------------------------ | ------------------------ |
| Staging     | `staging.wiselinetrade.com` | `WiseLinePortal-Staging` | `WiseLinePortal-Staging` | `WiseLinePortal_Staging` |
| Production  | `wiselinetrade.com`         | `WiseLinePortal`         | `WiseLinePortal`         | `WiseLinePortal`         |

Use `No Managed Code`, Integrated pipeline, AlwaysRunning, and a dedicated identity for each app pool. Bind a trusted TLS certificate and redirect HTTP to HTTPS.

## External configuration

The application reads an external JSON file named by the app-pool environment variable `WISELINE_PORTAL_CONFIG_FILE`. Configure it once per pool, for example:

```powershell
& $env:windir\System32\inetsrv\appcmd.exe set config `
  -section:system.applicationHost/applicationPools `
  /+"[name='WiseLinePortal'].environmentVariables.[name='WISELINE_PORTAL_CONFIG_FILE',value='C:\WiseLinePortal\Config\Production\appsettings.External.json']" `
  /commit:apphost
```

Use a different path and pool name for staging. The deployment workflow rewrites that file and restricts it to Administrators, SYSTEM, and the selected app-pool identity.

## GitHub environments

Create `staging` and `production` environments with branch restrictions matching their branch.

Environment variables:

| Variable               | Example                                                         |
| ---------------------- | --------------------------------------------------------------- |
| `PUBLIC_BASE_URL`      | `https://wiselinetrade.com`                                     |
| `IIS_SITE_NAME`        | `WiseLinePortal`                                                |
| `APP_POOL_NAME`        | `WiseLinePortal`                                                |
| `RELEASE_ROOT`         | `C:\WiseLinePortal\Releases\Production`                         |
| `CONFIG_PATH`          | `C:\WiseLinePortal\Config\Production\appsettings.External.json` |
| `HEALTH_URL`           | `https://wiselinetrade.com/health/live`                         |
| `PORTAL_DATABASE_NAME` | `WiseLinePortal`                                                |
| `SQL_BACKUP_DIRECTORY` | SQL-server-local backup directory                               |
| `PAYPAL_BASE_URL`      | sandbox for staging, live for production                        |

Environment secrets:

- `PORTAL_MIGRATION_DATABASE_CONNECTION_STRING` — deployment owner connection used only by migration and backup
- `PORTAL_RUNTIME_DATABASE_CONNECTION_STRING` — least-privilege runtime connection used by the IIS application
- `TRADE_DATABASE_CONNECTION_STRING` — restricted portal connector
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`
- `STRIPE_SECRET_KEY`, `STRIPE_PRICE_ID`, `STRIPE_WEBHOOK_SECRET`
- `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_PLAN_ID`, `PAYPAL_WEBHOOK_ID`

Never place the deployment-owner connection in the external application configuration. The workflow exposes it only to the backup and migration steps.

## Release and rollback behavior

The build job publishes Angular into the ASP.NET Core `wwwroot`, creates a framework-dependent Windows package, creates a self-contained EF migration bundle, and records a SHA-256 checksum. The deployment job verifies the checksum, backs up production, applies migrations, expands the package into a commit-SHA release directory, switches the IIS physical path, and runs the live health check.

If the health check fails, the deployment script restores the previous IIS physical path automatically. The manual `Roll back IIS release` workflow switches to any retained commit-SHA release. Database migrations are not automatically reversed; use backward-compatible expand/contract migrations and forward fixes.
