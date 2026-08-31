# Architecture

WiseLine Portal is a modular web application with a strict integration boundary around the existing investment platform.

## Runtime components

```text
Browser / mobile browser
        |
        | HTTPS, secure Identity cookie
        v
Angular application + ASP.NET Core API (IIS)
        |                         |
        | EF Core                | execute approved invest.* procedures
        v                         v
WiseLinePortal database          Trade database
auth, billing, audit,            portfolios, positions, strategies,
preferences, provider events     investment identities and MCP tokens
                                  ^
                                  |
                            Investment MCP service
```

`investment_portal` and `investment_mcp` remain separate repositories, deployment units, and processes. The portal never calls the Investment MCP HTTP endpoint and does not import its source code. The `Trade` database is the integration boundary.

## Database ownership

`WiseLinePortal` owns:

- ASP.NET Core Identity accounts and Google-login associations
- portal profiles and settings
- the $10 monthly subscription state and 14-day trial state
- promotion-code definitions and one-time redemptions
- idempotent Stripe and PayPal webhook records
- audit events and the portal-to-Trade identity link

`Trade` remains the source of truth for:

- investment users and MCP access tokens
- portfolios, positions, strategies, and market-derived values
- Investment MCP entitlements used by that service

The API uses separate connection strings and never performs cross-database joins. The Trade runtime identity receives `EXECUTE` only on the approved procedures documented in `TRADE_DATABASE_CONTRACT.md`.

## Authentication and browser security

- Email/password accounts use ASP.NET Core Identity with a 12-character complexity policy and lockout.
- Google uses the standard OpenID Connect/OAuth handler.
- The browser session is an HttpOnly, Secure, SameSite=Lax cookie.
- State-changing API requests require an antiforgery token.
- Authentication tokens are never stored in browser local storage.
- General API traffic is limited to 60 requests per minute per user or source IP.

## Subscription lifecycle

1. Registration creates a `Pending` subscription with no entitlement.
2. The user chooses Stripe or PayPal checkout.
3. The provider collects a payment method and creates the subscription.
4. A verified, idempotent provider webhook starts the 14-day trial.
5. The webhook state controls portal entitlement; browser redirects are never trusted as payment proof.
6. At trial end the provider bills $10 monthly. Past-due, canceled, and expired events remove entitlement.

## Database changes

EF Core migrations are generated in `WiseLine.Portal.Infrastructure/Persistence/Migrations`. The application never migrates at startup. CI creates a pinned Windows migration bundle; deployment runs that bundle before switching IIS to the new application release.
