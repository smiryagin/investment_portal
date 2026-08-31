using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseLine.Portal.Api.Contracts;
using WiseLine.Portal.Api.Security;
using WiseLine.Portal.Application.Subscriptions;
using WiseLine.Portal.Application.Trade;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/mcp-tokens")]
public sealed class McpAccessController(
    ITradePortalGateway tradeGateway,
    ISubscriptionAccessService subscriptionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<McpTokenSummary>>> GetAll(
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        if (!await IsEntitledAsync(userId, cancellationToken))
        {
            return PaymentRequired();
        }

        try
        {
            return Ok(await tradeGateway.GetMcpTokensAsync(userId, cancellationToken));
        }
        catch (TradeIntegrationUnavailableException exception)
        {
            return TradeUnavailable(exception);
        }
    }

    [HttpPost]
    public async Task<ActionResult<McpTokenCreated>> Create(
        CreateMcpTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        if (!await IsEntitledAsync(userId, cancellationToken))
        {
            return PaymentRequired();
        }

        try
        {
            var result = await tradeGateway.CreateMcpTokenAsync(
                userId,
                request.DisplayName,
                cancellationToken);
            return Created($"/api/mcp-tokens/{result.Id}", result);
        }
        catch (TradeIntegrationUnavailableException exception)
        {
            return TradeUnavailable(exception);
        }
    }

    [HttpDelete("{tokenId:long}")]
    public async Task<IActionResult> Revoke(long tokenId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        if (!await IsEntitledAsync(userId, cancellationToken))
        {
            return PaymentRequired();
        }

        try
        {
            await tradeGateway.RevokeMcpTokenAsync(userId, tokenId, cancellationToken);
            return NoContent();
        }
        catch (TradeIntegrationUnavailableException exception)
        {
            return TradeUnavailable(exception);
        }
    }

    private async Task<bool> IsEntitledAsync(Guid userId, CancellationToken cancellationToken) =>
        (await subscriptionService.GetOrCreateAsync(userId, cancellationToken)).IsEntitled;

    private ObjectResult PaymentRequired() => Problem(
        statusCode: StatusCodes.Status402PaymentRequired,
        title: "Subscription required",
        detail: "An active subscription or trial is required to manage MCP access.");

    private ObjectResult TradeUnavailable(Exception exception) => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Investment MCP access is temporarily unavailable",
        detail: exception.Message);
}
