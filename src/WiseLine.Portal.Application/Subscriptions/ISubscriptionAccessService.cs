namespace WiseLine.Portal.Application.Subscriptions;

using WiseLine.Portal.Domain.Subscriptions;

public interface ISubscriptionAccessService
{
    Task<SubscriptionSnapshot> GetOrCreateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionSnapshot> RedeemPromotionCodeAsync(
        Guid userId,
        string promotionCode,
        CancellationToken cancellationToken = default);

    Task<SubscriptionBillingReference?> GetBillingReferenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record SubscriptionBillingReference(
    PaymentProvider Provider,
    string ProviderCustomerId,
    string ProviderSubscriptionId);
