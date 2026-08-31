namespace WiseLine.Portal.Domain.Subscriptions;

public sealed class PromotionRedemption
{
    private PromotionRedemption()
    {
    }

    public PromotionRedemption(Guid promotionCodeId, Guid userId, DateTimeOffset redeemedAt)
    {
        Id = Guid.NewGuid();
        PromotionCodeId = promotionCodeId;
        UserId = userId;
        RedeemedAt = redeemedAt;
    }

    public Guid Id { get; private set; }

    public Guid PromotionCodeId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }
}
