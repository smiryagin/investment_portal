using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseLine.Portal.Api.Contracts;
using WiseLine.Portal.Api.Security;
using WiseLine.Portal.Application.Subscriptions;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/subscription")]
public sealed class SubscriptionsController(ISubscriptionAccessService subscriptionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SubscriptionSnapshot>> Get(CancellationToken cancellationToken)
    {
        var result = await subscriptionService.GetOrCreateAsync(
            User.GetRequiredUserId(),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("promotion")]
    public async Task<ActionResult<SubscriptionSnapshot>> RedeemPromotion(
        RedeemPromotionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await subscriptionService.RedeemPromotionCodeAsync(
                User.GetRequiredUserId(),
                request.Code,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Promotion code could not be redeemed",
                detail: exception.Message);
        }
    }
}
