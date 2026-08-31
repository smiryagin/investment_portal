using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Api.Contracts;
using WiseLine.Portal.Api.Security;
using WiseLine.Portal.Application.Payments;
using WiseLine.Portal.Application.Subscriptions;
using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Infrastructure.Identity;
using WiseLine.Portal.Infrastructure.Payments;

namespace WiseLine.Portal.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    IPaymentCheckoutService checkoutService,
    IPaymentWebhookProcessor webhookProcessor,
    ISubscriptionAccessService subscriptionService,
    UserManager<PortalUser> userManager,
    IOptions<PaymentOptions> options) : ControllerBase
{
    [Authorize]
    [HttpPost("checkout/{provider}")]
    public async Task<ActionResult<CheckoutResponse>> CreateCheckout(
        PaymentProvider provider,
        CancellationToken cancellationToken)
    {
        if (provider is not PaymentProvider.Stripe and not PaymentProvider.PayPal)
        {
            return BadRequest(new ProblemDetails { Title = "Unsupported payment provider" });
        }

        var userId = User.GetRequiredUserId();
        var subscription = await subscriptionService.GetOrCreateAsync(userId, cancellationToken);
        if (!string.Equals(subscription.Status, SubscriptionStatus.Pending.ToString(), StringComparison.Ordinal))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Subscription already started",
                Detail = "Checkout is available only before the trial or paid subscription begins."
            });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user?.Email is null)
        {
            return Unauthorized();
        }

        var baseUri = new Uri(options.Value.PublicBaseUrl.TrimEnd('/') + "/");
        var returnUrl = new Uri(baseUri, "account?checkout=success");
        var cancelUrl = new Uri(baseUri, "account?checkout=canceled");

        try
        {
            var session = await checkoutService.CreateCheckoutAsync(
                userId,
                user.Email,
                provider,
                returnUrl,
                cancelUrl,
                cancellationToken);
            return Ok(new CheckoutResponse(session.RedirectUrl.ToString(), session.ExternalSessionId));
        }
        catch (PaymentProviderUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Checkout is temporarily unavailable",
                detail: exception.Message);
        }
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [DisableRateLimiting]
    [RequestSizeLimit(1_048_576)]
    [HttpPost("webhooks/stripe")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await webhookProcessor.ProcessStripeAsync(payload, signature, cancellationToken);
            return Ok();
        }
        catch (InvalidWebhookSignatureException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid webhook signature", Detail = exception.Message });
        }
        catch (PaymentProviderUnavailableException exception)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Stripe webhook unavailable", detail: exception.Message);
        }
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [DisableRateLimiting]
    [RequestSizeLimit(1_048_576)]
    [HttpPost("webhooks/paypal")]
    public async Task<IActionResult> PayPalWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        try
        {
            await webhookProcessor.ProcessPayPalAsync(payload, headers, cancellationToken);
            return Ok();
        }
        catch (InvalidWebhookSignatureException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid webhook signature", Detail = exception.Message });
        }
        catch (PaymentProviderUnavailableException exception)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "PayPal webhook unavailable", detail: exception.Message);
        }
    }
}
