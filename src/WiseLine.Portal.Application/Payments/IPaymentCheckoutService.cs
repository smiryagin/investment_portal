using WiseLine.Portal.Domain.Subscriptions;

namespace WiseLine.Portal.Application.Payments;

public interface IPaymentCheckoutService
{
    Task<CheckoutSession> CreateCheckoutAsync(
        Guid userId,
        string email,
        PaymentProvider provider,
        Uri returnUrl,
        Uri cancelUrl,
        CancellationToken cancellationToken = default);
}

public sealed record CheckoutSession(Uri RedirectUrl, string ExternalSessionId);
