# Trade database portal contract

The portal runtime login must not receive direct table permissions. Grant `EXECUTE` on the approved `invest` procedures only. The contract below is implemented by `TradePortalGateway`; creating these procedures belongs to the separately reviewed `investment_mcp`/Trade workstream.

Portfolio and token procedures receive `@TradeUserId uniqueidentifier`. `TradePortalGateway` resolves this value server-side from `integration.InvestmentIdentityLinks` by using the authenticated portal user's claim. The browser never supplies or chooses a Trade user identifier.

## Identity and entitlement synchronization

`invest.Portal_EnsureUser` receives `@PortalUserId`, `@DisplayName`, and `@Email`. It idempotently creates or resolves the Trade user whose authentication subject is `portal:{PortalUserId}` and returns `TradeUserId`.

`invest.Portal_SetEntitlement` receives `@TradeUserId`, `@PortalUserId`, `@IsEntitled`, and nullable `@EntitledThrough`. Trade stores this boundary so Investment MCP authentication expires locally without querying WiseLinePortal. Existing Trade users that have no portal entitlement link are unaffected.

WiseLinePortal queues every subscription change in `integration.TradeEntitlementSyncRequests`. A background worker retries synchronization after temporary failures. Every interactive portfolio or token request also synchronizes current entitlement before reading or writing Trade data.

## `invest.Portal_GetPortfolios`

Returns one result set:

| Column             | SQL type           | Nullable |
| ------------------ | ------------------ | -------- |
| `PortfolioId`      | `uniqueidentifier` | no       |
| `Name`             | `nvarchar`         | no       |
| `StrategyName`     | `nvarchar`         | yes      |
| `MarketValue`      | `decimal`          | no       |
| `CashBalance`      | `decimal`          | no       |
| `DayChange`        | `decimal`          | no       |
| `DayChangePercent` | `decimal`          | no       |
| `PositionCount`    | `int`              | no       |
| `UpdatedAt`        | `datetimeoffset`   | yes      |

## `invest.Portal_GetPortfolio`

Additional parameter: `@PortfolioId uniqueidentifier`.

The first result set is one authorized portfolio row with `PortfolioId`, `Name`, `Description`, `StrategyName`, `MarketValue`, `CashBalance`, `TotalCost`, `UnrealizedGain`, and `UnrealizedGainPercent`.

`CashBalance` is the account's USD `TotalAmount`. `MarketValue` is total portfolio value: USD cash plus the market value of security positions. `TotalCost`, `UnrealizedGain`, and `UnrealizedGainPercent` continue to describe security positions only, so adding cash does not manufacture an investment gain or loss.

The second result set contains authorized positions with `PositionId`, `Symbol`, `Description`, `Quantity`, `AveragePrice`, `CurrentPrice`, `MarketValue`, `UnrealizedGain`, and `UnrealizedGainPercent`. `PositionId` is `nvarchar(50)` and is the normalized symbol, which is the stable position key inside one account.

The procedure must return no row when the portfolio does not belong to the resolved user.

Apply `investment_mcp/sql/019_add_portal_cash_valuation.sql` to an existing Trade database before deploying a portal build that expects `CashBalance`.

## `invest.Portal_GetMcpTokens`

Returns `TokenId` (`uniqueidentifier`), `DisplayName`, `TokenPrefix`, `CreatedAt`, `LastUsedAt`, `ExpiresAt`, and `IsRevoked`. It must never return token hashes or complete token values.

## `invest.Portal_CreateMcpToken`

Additional parameter: `@DisplayName nvarchar(100)`.

Creates a cryptographically random, hashed-at-rest token for the resolved user. Returns one row with `TokenId`, `DisplayName`, `Token`, `TokenPrefix`, `CreatedAt`, and `ExpiresAt`. `Token` is returned exactly once.

The Trade implementation must enforce the agreed per-user token limit, entitlement rules, and case-insensitive uniqueness of active token display names transactionally. A display name may be reused after its previous token is revoked or expires.

## `invest.Portal_RevokeMcpToken`

Additional parameter: `@TokenId uniqueidentifier`.

Revokes only a token owned by the resolved user. The operation is idempotent.

## Runtime login setup

Migration `sql/012_add_portal_integration.sql` creates the `investment_portal_runtime` database role and grants only the contract procedures. Create the SQL login with a strong secret outside source control, then map it in `Trade`:

```sql
USE [master];
CREATE LOGIN [InvestmentPortal_Connector]
    WITH PASSWORD = N'REPLACE_WITH_A_STRONG_RANDOM_PASSWORD',
         CHECK_POLICY = ON,
         CHECK_EXPIRATION = OFF;

USE [Trade];
CREATE USER [InvestmentPortal_Connector]
    FOR LOGIN [InvestmentPortal_Connector];
ALTER ROLE [investment_portal_runtime]
    ADD MEMBER [InvestmentPortal_Connector];
```

Store the password in the staging/production secret configuration, never in Git. Do not add the connector to `db_datareader`, `db_datawriter`, or `db_owner`.
