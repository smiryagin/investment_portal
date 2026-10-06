using System.Security.Claims;
using System.Collections.Immutable;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using WiseLine.Portal.Api.Models;
using WiseLine.Portal.Infrastructure.Identity;
using WiseLine.Portal.Infrastructure.OAuth;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WiseLine.Portal.Api.Controllers;

public sealed class OAuthController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    UserManager<PortalUser> userManager,
    IOptions<OAuthServerOptions> options,
    ILogger<OAuthController> logger) : Controller
{
    [AllowAnonymous]
    [EnableRateLimiting("oauth-authorization")]
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth authorization request is unavailable.");
        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
            return LocalRedirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.ClientId ?? string.Empty,
            cancellationToken);
        if (application is null)
        {
            return OAuthForbid(Errors.InvalidClient, "The requesting AI client is not approved.");
        }

        var resourceError = ValidateResource(request);
        if (resourceError is not null)
        {
            return resourceError;
        }

        var displayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken)
            ?? request.ClientId
            ?? "AI client";
        return View(new OAuthAuthorizationViewModel(displayName, DescribeScopes(request.GetScopes())));
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("oauth-authorization")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> AuthorizeDecision(
        [FromForm] string decision,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth authorization request is unavailable.");
        if (!string.Equals(decision, "allow", StringComparison.Ordinal))
        {
            logger.LogInformation(
                "OAuth authorization was denied by subject {Subject} for client {ClientId}.",
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
                request.ClientId ?? "unknown");
            return OAuthForbid(Errors.AccessDenied, "The user denied this connection request.");
        }

        var resourceError = ValidateResource(request);
        if (resourceError is not null)
        {
            return resourceError;
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge(IdentityConstants.ApplicationScheme);
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.ClientId ?? string.Empty,
            cancellationToken);
        if (application is null)
        {
            return OAuthForbid(Errors.InvalidClient, "The requesting AI client is not approved.");
        }

        var applicationId = await applicationManager.GetIdAsync(application, cancellationToken)
            ?? throw new InvalidOperationException("The OAuth application identifier is unavailable.");
        var scopes = request.GetScopes();
        var principal = CreatePrincipal(user, scopes);
        principal.SetResources(options.Value.Resource);
        var authorization = await FindPermanentAuthorizationAsync(
            principal.GetClaim(Claims.Subject)!,
            applicationId,
            scopes,
            cancellationToken);
        authorization ??= await authorizationManager.CreateAsync(
            principal,
            principal.GetClaim(Claims.Subject)!,
            applicationId,
            AuthorizationTypes.Permanent,
            scopes,
            cancellationToken);
        principal.SetAuthorizationId(
            await authorizationManager.GetIdAsync(authorization, cancellationToken));
        logger.LogInformation(
            "OAuth authorization was granted by portal user {PortalUserId} to client {ClientId} for scopes {Scopes}.",
            user.Id,
            request.ClientId,
            string.Join(' ', scopes));
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("oauth-token")]
    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth token request is unavailable.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return OAuthForbid(Errors.UnsupportedGrantType, "The requested grant type is not supported.");
        }

        var result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var authenticatedPrincipal = result.Principal;
        var subject = authenticatedPrincipal?.GetClaim(Claims.Subject);
        if (authenticatedPrincipal is null || !TryGetPortalUserId(subject, out var portalUserId))
        {
            return OAuthForbid(Errors.InvalidGrant, "The authorization grant is no longer valid.");
        }

        var user = await userManager.FindByIdAsync(portalUserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return OAuthForbid(Errors.InvalidGrant, "The WiseLine account is no longer available.");
        }

        var principal = CreatePrincipal(user, authenticatedPrincipal.GetScopes());
        principal.SetResources(options.Value.Resource);
        principal.SetAuthorizationId(authenticatedPrincipal.GetAuthorizationId());
        logger.LogInformation(
            "OAuth {GrantType} exchange succeeded for portal user {PortalUserId} and client {ClientId}.",
            request.GrantType,
            user.Id,
            request.ClientId ?? "unknown");
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private IActionResult? ValidateResource(OpenIddictRequest request)
    {
        var resources = request.GetResources();
        if (resources.Length != 1 ||
            !string.Equals(resources[0], options.Value.Resource, StringComparison.Ordinal))
        {
            return OAuthForbid(
                Errors.InvalidTarget,
                "The request must target the configured WiseLine Investment MCP resource.");
        }

        return null;
    }

    private static ClaimsPrincipal CreatePrincipal(PortalUser user, IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        identity.SetClaim(Claims.Subject, $"portal:{user.Id:D}".ToLowerInvariant());
        identity.SetClaim(Claims.JwtId, Guid.NewGuid().ToString("N"));
        identity.SetClaim(Claims.Name, user.DisplayName);
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.SetClaim(Claims.Email, user.Email);
        }

        identity.SetScopes(scopes);
        identity.SetDestinations(static claim =>
            claim.Type is Claims.Subject or Claims.JwtId or Claims.Name or Claims.Email
                ? [Destinations.AccessToken]
                : []);
        return new ClaimsPrincipal(identity);
    }

    private static IReadOnlyList<OAuthScopeViewModel> DescribeScopes(IEnumerable<string> scopes) =>
        scopes
            .Where(scope => scope is "investments.read" or "investments.write" or Scopes.OfflineAccess)
            .Distinct(StringComparer.Ordinal)
            .Select(scope => scope switch
            {
                "investments.read" => new OAuthScopeViewModel(
                    scope,
                    "View your investment accounts, portfolios, positions, research, and strategies."),
                "investments.write" => new OAuthScopeViewModel(
                    scope,
                    "Create and update portfolios, positions, order records, cash, and strategies."),
                _ => new OAuthScopeViewModel(
                    scope,
                    "Stay connected without asking you to sign in for every session.")
            })
            .ToArray();

    private async Task<object?> FindPermanentAuthorizationAsync(
        string subject,
        string applicationId,
        ImmutableArray<string> scopes,
        CancellationToken cancellationToken)
    {
        await foreach (var authorization in authorizationManager.FindAsync(
            subject,
            applicationId,
            Statuses.Valid,
            AuthorizationTypes.Permanent,
            scopes,
            cancellationToken))
        {
            return authorization;
        }

        return null;
    }

    private static bool TryGetPortalUserId(string? subject, out Guid portalUserId) =>
        Guid.TryParse(subject?.StartsWith("portal:", StringComparison.Ordinal) == true
            ? subject["portal:".Length..]
            : null, out portalUserId);

    private ForbidResult OAuthForbid(string error, string description) =>
        Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
