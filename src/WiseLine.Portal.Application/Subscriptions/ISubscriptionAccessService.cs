namespace WiseLine.Portal.Application.Subscriptions;

public interface ISubscriptionAccessService
{
    Task<SubscriptionSnapshot> GetOrCreateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionSnapshot> RedeemPromotionCodeAsync(
        Guid userId,
        string promotionCode,
        CancellationToken cancellationToken = default);
}
