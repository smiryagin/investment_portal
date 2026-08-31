# WiseLine Trade Portal

WiseLine Trade is the customer portal for subscribing to the WiseLine Investment MCP service, managing access, and viewing investment portfolios. The portal and Investment MCP are separate applications. They exchange investment state only through approved stored procedures in the existing `Trade` database.

## Technology

- Angular 22 and TypeScript 6
- ASP.NET Core 10 Web API
- Entity Framework Core 10
- SQL Server (`WiseLinePortal` for portal data, restricted read/execute access to `Trade`)
- IIS on Windows Server
- GitHub Actions for CI and staged deployments

## Repository layout

```text
src/
  WiseLine.Portal.Api/             ASP.NET Core host and HTTP API
  WiseLine.Portal.Application/     use-case contracts and DTOs
  WiseLine.Portal.Domain/          portal business model
  WiseLine.Portal.Infrastructure/  SQL Server, Identity, and Trade integration
  WiseLine.Portal.Web/             Angular application
tests/                             .NET test projects
tools/mcp-wiselineportal-db/       local development-only database MCP
```

## Local prerequisites

- .NET SDK 10.0.202 or a compatible later feature band
- Node.js 24.15.0 (see `.nvmrc`)
- SQL Server access supplied through environment variables or .NET user-secrets

## Build

```powershell
dotnet restore WiseLine.Portal.sln
dotnet build WiseLine.Portal.sln --configuration Release --no-restore

cd src\WiseLine.Portal.Web
npm ci
npm run build
npm test -- --watch=false
```

The API never runs database migrations automatically. Deployment applies reviewed migrations before switching the IIS site to a new release.

## Local configuration

Keep credentials outside Git. The API reads standard .NET configuration keys, so double underscores map environment variables to nested settings:

```text
ConnectionStrings__PortalDatabase
ConnectionStrings__TradeDatabase
Authentication__Google__ClientId
Authentication__Google__ClientSecret
```

Use a deployment identity only for migrations. The running API must use separate least-privilege identities for `WiseLinePortal` and `Trade`.

## Project documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Trade database stored-procedure contract](docs/TRADE_DATABASE_CONTRACT.md)
- [Database identities and permissions](docs/DATABASE_SECURITY.md)
- [Stripe and PayPal setup](docs/PAYMENTS.md)
- [GitHub Actions and IIS deployment](docs/DEPLOYMENT.md)
