# MCP OAuth connection design

Status: design spike, approved for investigation only. This document does not authorize a database migration or deployment.

## Decision

WiseLine Trade will act as the OAuth authorization server for the Investment MCP resource server. The portal will use OpenIddict 7.7.x on top of the existing ASP.NET Core Identity accounts. The Investment MCP service will remain a separate deployment and will validate portal-issued JWT access tokens using the portal's public JSON Web Key Set (JWKS).

The first release will support the published Client ID Metadata Document (CIMD) identities used by the major AI hosts we explicitly approve. It will not expose unauthenticated Dynamic Client Registration (DCR). Existing `imcp_...` API tokens will remain available under an **Advanced / manual setup** section during the transition.

This decision keeps the current portal accounts, Google login, subscription data, and Trade identity mapping intact. It also avoids implementing OAuth itself: OpenIddict owns the authorization-code, PKCE, token, refresh-token, revocation, discovery, signing, and persistence mechanics.

## User experience

The default connection flow becomes:

1. The user adds `https://investments-mcp.torusystems.com/mcp` in a supported AI client or installs the published WiseLine plugin.
2. The client discovers the Investment MCP protected-resource metadata and WiseLine authorization server.
3. The browser opens WiseLine Trade.
4. The user signs in with their WiseLine email/password or Google account.
5. WiseLine displays the requesting AI client, requested permissions, and the Investment MCP resource.
6. The user approves access.
7. The AI client receives and stores OAuth tokens, then retries the MCP connection automatically.

The portal never displays an OAuth access token to the user. Manual API-token creation remains an advanced fallback for clients that cannot perform MCP OAuth.

## System boundary

```text
AI client
  |  OAuth discovery + authorization code/PKCE
  v
WiseLine Portal / Authorization Server
  |  existing ASP.NET Identity account
  |  existing PortalUserId -> TradeUserId mapping
  |  signed JWT access token (no Trade data access by client)
  v
Investment MCP / Resource Server
  |  validate signature, issuer, audience, expiry, scopes
  |  resolve subject and current entitlement
  v
Trade database
```

The Investment MCP service must not connect to the `WiseLinePortal` database. Subscription and entitlement state continues to cross the project boundary through the existing Trade synchronization contract.

## Environment contract

Staging and production require different issuers, signing keys, OAuth stores, and MCP resource identifiers. A staging token must never be accepted by production.

| Setting | Staging | Production |
| --- | --- | --- |
| OAuth issuer | `https://staging.wiselinetrade.com` | `https://wiselinetrade.com` |
| MCP resource | Dedicated staging MCP URL, to be created | `https://investments-mcp.torusystems.com/mcp` |
| signing certificate | staging-only certificate | production-only certificate |
| portal database | `WiseLinePortal_Staging` | `WiseLinePortal` |

The staging MCP hostname is an infrastructure prerequisite for end-to-end testing. Reusing the production MCP resource for staging would weaken audience and issuer isolation.

Issuer strings are immutable protocol identifiers. Their trailing slash, casing, host, port, and path must match exactly in discovery, the `iss` authorization response parameter, and token validation.

## Authorization-server surface

OpenIddict will provide the protocol implementation behind these public endpoints:

| Endpoint | Purpose |
| --- | --- |
| `/.well-known/oauth-authorization-server` | OAuth authorization-server metadata |
| `/.well-known/openid-configuration` | OIDC discovery compatibility |
| `/.well-known/jwks` | public signing keys |
| `/connect/authorize` | authorization code + PKCE request |
| `/connect/token` | code and refresh-token exchange |
| `/connect/revoke` | refresh/access token revocation |

The metadata must advertise at least:

- `response_types_supported: ["code"]`
- `grant_types_supported: ["authorization_code", "refresh_token"]`
- `code_challenge_methods_supported: ["S256"]`
- `token_endpoint_auth_methods_supported` including `none` for public AI clients
- `authorization_response_iss_parameter_supported: true`
- `client_id_metadata_document_supported: true` only if WiseLine later implements runtime CIMD retrieval and validation
- `scopes_supported` containing the WiseLine scopes and `offline_access`

Every successful and error authorization response must include an exact `iss` value. The `resource` parameter must be accepted at authorization and token endpoints, retained with the authorization code, and represented by the access-token `aud` claim.

## Scopes

Initial scopes are deliberately small:

| Scope | Grants |
| --- | --- |
| `investments.read` | market data, research, accounts, portfolios, positions, strategies, sharing lists |
| `investments.write` | create/update portfolios, position imports, order records, cash, strategies, and portfolio sharing |
| `offline_access` | refresh token for a persistent AI connection |

`trading.execute` is reserved for a future feature that actually submits broker orders. The current order tools maintain Trade records; they do not execute a Schwab order, so they remain under `investments.write`.

Consent must show read and write permissions separately. A later client requesting a new scope must receive a new consent prompt.

## Access-token contract

Access tokens are signed JWTs and are not encrypted, because the separately deployed MCP resource server must validate them locally. Authorization codes and refresh tokens remain protected and stored by OpenIddict.

Required access-token claims:

| Claim | Value |
| --- | --- |
| `iss` | exact environment issuer |
| `aud` | exact environment MCP resource URL |
| `sub` | `portal:<lowercase PortalUserId>` |
| `scope` | granted space-delimited scopes |
| `client_id` or `azp` | approved AI client identity |
| `iat`, `nbf`, `exp`, `jti` | standard issuance, lifetime, and replay/audit data |

Recommended lifetimes:

- access token: 10 minutes
- refresh token: rotating, 30-day inactivity lifetime, 90-day absolute lifetime
- authorization code: 5 minutes, single use

The MCP server must check the current Trade entitlement on every authenticated request. A validly signed access token alone is not sufficient after a cancellation, expired trial, disabled user, or entitlement revocation.

## Client registration strategy

OpenIddict 7.7.x supports the required authorization code, PKCE, refresh-token, discovery/JWKS, issuer-identification, and resource-indicator features. It does not currently implement RFC 7591 DCR. It also does not automatically resolve arbitrary CIMD URL client identifiers.

For the first release, a deployment-time importer will pre-register a small set
of published client identities. This is the **predefined client** compatibility
path; importing a document does not by itself make WiseLine a general CIMD
authorization server, so discovery must not advertise
`client_id_metadata_document_supported: true` in this phase.

The importer will:

1. accept only an explicit allowlist of HTTPS CIMD URLs;
2. fetch without redirects and with DNS/IP protections against SSRF;
3. validate that `client_id` exactly equals the document URL;
4. validate token authentication method (`none` initially), grant/response types, and redirect URIs;
5. upsert the resulting public application in the OpenIddict application store;
6. record the document digest and last successful refresh for audit.

Initial predefined-client candidates:

- OpenAI published identity: `https://chatgpt.com/oauth/client.json`
- Claude published identity: import the exact URL shown by Claude's connector configuration; do not guess it

Unknown CIMD clients are rejected. Older DCR-only clients can use an administrator-created static OAuth client or the advanced `imcp_...` token path.

If broad, zero-touch DCR becomes a product requirement, use a managed authorization service with first-class MCP support (currently Auth0 is the leading candidate) rather than writing an open registration endpoint. That would be a separate identity-migration decision.

## Portal data changes planned for the implementation phase

No schema change is part of this spike. The implementation migration will add OpenIddict's application, authorization, scope, and token stores to the portal database, preferably in an `oauth` schema. OpenIddict authorizations will represent durable user consent.

The implementation should not create a second WiseLine user table. OpenIddict principals use the existing `auth.Users` ASP.NET Identity records. Existing email/password and Google login continue unchanged.

The account page will later add **Connected AI clients**, showing client name, granted scopes, first/last authorization, and a revoke action. Manual MCP tokens move under **Advanced access tokens**.

## Signing keys

Each environment uses a dedicated RSA signing certificate from the Windows certificate store. Development may use an ephemeral development certificate only on a developer machine.

Planned secret/configuration split:

- non-secret: issuer, resource URL, access-token lifetime, allowed CIMD URLs
- secret/sensitive: signing-certificate private-key access and any future confidential client credentials
- public: JWKS signing keys

Production must not load a signing private key from source control or `appsettings.json`. The IIS app-pool identity receives read access only to that certificate's private key.

## Security requirements

- Require PKCE `S256`; never allow `plain`.
- Public clients use token endpoint authentication method `none`.
- Match redirect URIs by exact string.
- Validate `resource` and issue the exact MCP audience.
- Include and validate RFC 9207 `iss` on callbacks.
- Rotate refresh tokens and detect reuse.
- Revoke a client's authorizations and refresh tokens when the user disconnects it.
- Do not put subscriptions, portfolio details, email addresses, payment data, or secrets in access tokens.
- Rate-limit authorization, token, and consent endpoints independently.
- Audit grants, denials, refreshes, revocations, and CIMD imports without logging tokens or authorization codes.
- Preserve the portal's antiforgery, secure-cookie, CSP, forwarded-header, and Data Protection configuration.

## Rollout plan requiring separate approval

1. Create the dedicated staging MCP hostname/resource.
2. Add OpenIddict packages and the portal OAuth migration.
3. Add authorization, consent, token, revocation, discovery, JWKS, and certificate configuration.
4. Add the allowlisted CIMD importer and seed the OpenAI/Claude published identities.
5. Add JWT + legacy-token hybrid authentication to Investment MCP.
6. Add protected-resource metadata, tool-level security metadata, and the profile tool.
7. Run metadata-contract, invalid-token, scope, audience, entitlement, refresh-rotation, and revocation tests locally.
8. Review and apply the portal and Trade migrations explicitly.
9. Deploy to staging only and test Codex, ChatGPT, and Claude end to end.
10. Move manual tokens to Advanced only after OAuth succeeds across the supported clients.

## Acceptance criteria

- Adding only the MCP URL in a supported host opens a WiseLine sign-in page.
- Email/password and Google sign-in both resume the original authorization request.
- The consent page identifies the AI client and requested scopes.
- The client receives a resource-bound, short-lived token and can refresh it.
- The MCP server rejects wrong issuer, wrong audience, missing scope, expired token, and inactive entitlement.
- Revoking a connected client prevents refresh immediately and MCP access no later than the current access token lifetime; the Trade entitlement check may make it immediate.
- Existing manual tokens continue to work during transition.
- The MCP service never reads the WiseLinePortal database.

## Compatibility conclusion

OpenIddict is viable for the first WiseLine release when CIMD clients are allowlisted and imported. It is not a complete answer for arbitrary DCR clients. This limitation is acceptable because ChatGPT/Codex and Claude expose published client identities, and WiseLine retains manual tokens as a fallback. A managed provider should be reconsidered before promising open, zero-touch registration for every MCP client.
