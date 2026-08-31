using System.Data;
using Microsoft.EntityFrameworkCore;
using WiseLine.Portal.Application.Subscriptions;
using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Subscriptions;

public sealed class SubscriptionAccessService(
    PortalDbContext dbContext,
    TimeProvider timeProvider) : ISubscriptionAccessService
{
    public async Task<SubscriptionSnapshot> GetOrCreateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await dbContext.Subscriptions
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (subscription is null)
        {
            subscription = Subscription.CreatePending(userId, timeProvider.GetUtcNow());
            dbContext.Subscriptions.Add(subscription);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(subscription);
    }

    public async Task<SubscriptionSnapshot> RedeemPromotionCodeAsync(
        Guid userId,
        string promotionCode,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedCode = PromotionCode.Normalize(promotionCode);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var subscription = await dbContext.Subscriptions
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (subscription is null)
        {
            subscription = Subscription.CreatePending(userId, now);
            dbContext.Subscriptions.Add(subscription);
        }

        var code = await dbContext.PromotionCodes
            .SingleOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);

        if (code is null || !code.CanRedeemAt(now))
        {
            throw new InvalidOperationException("Promotion code is invalid or no longer available.");
        }

        var alreadyRedeemed = await dbContext.PromotionRedemptions
            .AnyAsync(
                x => x.PromotionCodeId == code.Id && x.UserId == userId,
                cancellationToken);

        if (alreadyRedeemed)
        {
            throw new InvalidOperationException("This promotion code has already been redeemed.");
        }

        code.RecordRedemption(now);
        subscription.ExtendTrial(code.TrialExtensionDays, now);
        dbContext.PromotionRedemptions.Add(new PromotionRedemption(code.Id, userId, now));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(subscription);
    }

    private SubscriptionSnapshot Map(Subscription subscription)
    {
        var now = timeProvider.GetUtcNow();

        return new SubscriptionSnapshot(
            subscription.PlanKey,
            subscription.Status.ToString(),
            subscription.IsEntitledAt(now),
            subscription.TrialEndsAt,
            subscription.CurrentPeriodEndsAt,
            subscription.CancelAtPeriodEnd,
            subscription.Provider == PaymentProvider.None ? null : subscription.Provider.ToString());
    }
}
