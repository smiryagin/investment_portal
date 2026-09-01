using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseLine.Portal.Api.Security;
using WiseLine.Portal.Application.Subscriptions;
using WiseLine.Portal.Application.Trade;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/portfolios")]
public sealed class PortfoliosController(
    ITradePortalGateway tradeGateway,
    ISubscriptionAccessService subscriptionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortfolioSummary>>> GetAll(
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        if (!await IsEntitledAsync(userId, cancellationToken))
        {
            return PaymentRequired();
        }

        try
        {
            return Ok(await tradeGateway.GetPortfoliosAsync(userId, cancellationToken));
        }
        catch (TradeIntegrationUnavailableException exception)
        {
            return TradeUnavailable(exception);
        }
    }

    [HttpGet("{portfolioId:guid}")]
    public async Task<ActionResult<PortfolioDetails>> Get(
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        if (!await IsEntitledAsync(userId, cancellationToken))
        {
            return PaymentRequired();
        }

        try
        {
            var result = await tradeGateway.GetPortfolioAsync(userId, portfolioId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
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
        detail: "An active subscription or trial is required to view investment data.");

    private ObjectResult TradeUnavailable(Exception exception) => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Investment data is temporarily unavailable",
        detail: exception.Message);
}
