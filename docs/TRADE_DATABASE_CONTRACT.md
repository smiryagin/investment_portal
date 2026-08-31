# Trade database portal contract

The portal runtime login must not receive direct table permissions. Grant `EXECUTE` on the approved `invest` procedures only. The contract below is implemented by `TradePortalGateway`; creating these procedures belongs to the separately reviewed `investment_mcp`/Trade workstream.

Every procedure receives `@PortalUserId uniqueidentifier` and must resolve it to the caller's Trade identity without trusting a user identifier supplied by the browser.

## `invest.Portal_GetPortfolios`

Returns one result set:

| Column             | SQL type         | Nullable |
| ------------------ | ---------------- | -------- |
| `PortfolioId`      | `bigint`         | no       |
| `Name`             | `nvarchar`       | no       |
| `StrategyName`     | `nvarchar`       | yes      |
| `MarketValue`      | `decimal`        | no       |
| `DayChange`        | `decimal`        | no       |
| `DayChangePercent` | `decimal`        | no       |
| `PositionCount`    | `int`            | no       |
| `UpdatedAt`        | `datetimeoffset` | yes      |

## `invest.Portal_GetPortfolio`

Additional parameter: `@PortfolioId bigint`.

The first result set is one authorized portfolio row with `PortfolioId`, `Name`, `Description`, `StrategyName`, `MarketValue`, `TotalCost`, `UnrealizedGain`, and `UnrealizedGainPercent`.

The second result set contains authorized positions with `PositionId`, `Symbol`, `Description`, `Quantity`, `AveragePrice`, `CurrentPrice`, `MarketValue`, `UnrealizedGain`, and `UnrealizedGainPercent`.

The procedure must return no row when the portfolio does not belong to the resolved user.

## `invest.Portal_GetMcpTokens`

Returns `TokenId`, `DisplayName`, `TokenPrefix`, `CreatedAt`, `LastUsedAt`, `ExpiresAt`, and `IsRevoked`. It must never return token hashes or complete token values.

## `invest.Portal_CreateMcpToken`

Additional parameter: `@DisplayName nvarchar(100)`.

Creates a cryptographically random, hashed-at-rest token for the resolved user. Returns one row with `TokenId`, `DisplayName`, `Token`, `TokenPrefix`, `CreatedAt`, and `ExpiresAt`. `Token` is returned exactly once.

The Trade implementation must enforce the agreed per-user token limit and entitlement rules transactionally.

## `invest.Portal_RevokeMcpToken`

Additional parameter: `@TokenId bigint`.

Revokes only a token owned by the resolved user. The operation is idempotent.

## Permission example

Run this in `Trade` as an administrator after the procedures have been reviewed:

```sql
GRANT EXECUTE ON OBJECT::invest.Portal_GetPortfolios TO InvestmentPortal_Connector;
GRANT EXECUTE ON OBJECT::invest.Portal_GetPortfolio TO InvestmentPortal_Connector;
GRANT EXECUTE ON OBJECT::invest.Portal_GetMcpTokens TO InvestmentPortal_Connector;
GRANT EXECUTE ON OBJECT::invest.Portal_CreateMcpToken TO InvestmentPortal_Connector;
GRANT EXECUTE ON OBJECT::invest.Portal_RevokeMcpToken TO InvestmentPortal_Connector;
```

Do not add the connector to `db_datareader`, `db_datawriter`, or `db_owner`.
