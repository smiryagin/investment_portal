using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using WiseLine.Portal.Api.Contracts;
using WiseLine.Portal.Api.Security;
using WiseLine.Portal.Infrastructure.OAuth;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/oauth/connections")]
public sealed class OAuthConnectionsController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOptions<OAuthServerOptions> options,
    ILogger<OAuthConnectionsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OAuthConnectionResponse>>> List(
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return Ok(Array.Empty<OAuthConnectionResponse>());
        }

        var subject = GetSubject();
        var connections = new List<OAuthConnectionResponse>();
        await foreach (var authorization in authorizationManager.FindBySubjectAsync(
            subject,
            cancellationToken))
        {
            if (!await authorizationManager.HasStatusAsync(
                    authorization,
                    Statuses.Valid,
                    cancellationToken) ||
                !await authorizationManager.HasTypeAsync(
                    authorization,
                    AuthorizationTypes.Permanent,
                    cancellationToken))
            {
                continue;
            }

            var applicationId = await authorizationManager.GetApplicationIdAsync(
                authorization,
                cancellationToken);
            var application = applicationId is null
                ? null
                : await applicationManager.FindByIdAsync(applicationId, cancellationToken);
            if (application is null)
            {
                continue;
            }

            var id = await authorizationManager.GetIdAsync(authorization, cancellationToken);
            var clientId = await applicationManager.GetClientIdAsync(application, cancellationToken);
            if (id is null || clientId is null)
            {
                continue;
            }

            connections.Add(new OAuthConnectionResponse(
                id,
                clientId,
                await applicationManager.GetDisplayNameAsync(application, cancellationToken) ?? clientId,
                await authorizationManager.GetScopesAsync(authorization, cancellationToken),
                await authorizationManager.GetCreationDateAsync(authorization, cancellationToken)));
        }

        return Ok(connections.OrderByDescending(connection => connection.AuthorizedAt).ToArray());
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Revoke(string id, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var authorization = await authorizationManager.FindByIdAsync(id, cancellationToken);
        if (authorization is null ||
            !string.Equals(
                await authorizationManager.GetSubjectAsync(authorization, cancellationToken),
                GetSubject(),
                StringComparison.Ordinal))
        {
            return NotFound();
        }

        if (!await authorizationManager.TryRevokeAsync(authorization, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "The AI connection could not be revoked.",
                Detail = "Refresh the page and try again."
            });
        }

        logger.LogInformation(
            "Portal user {PortalUserId} revoked OAuth authorization {AuthorizationId}.",
            User.GetRequiredUserId(),
            id);
        return NoContent();
    }

    private string GetSubject() => $"portal:{User.GetRequiredUserId():D}".ToLowerInvariant();
}
