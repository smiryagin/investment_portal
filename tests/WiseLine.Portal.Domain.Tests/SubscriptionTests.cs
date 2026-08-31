using WiseLine.Portal.Domain.Subscriptions;

namespace WiseLine.Portal.Domain.Tests;

public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PendingSubscription_IsNotEntitledBeforePaymentSetup()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);

        Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
        Assert.False(subscription.IsEntitledAt(Now));
        Assert.Null(subscription.TrialStartedAt);
        Assert.Null(subscription.TrialEndsAt);
    }

    [Fact]
    public void BeginTrial_UsesProviderConfirmedFourteenDayTrial()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);
        subscription.BeginTrial(PaymentProvider.Stripe, "cus_123", "sub_123", Now.AddDays(14), Now);

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Equal(Now.AddDays(14), subscription.TrialEndsAt);
        Assert.Equal(PlanCatalog.MonthlyPlanKey, subscription.PlanKey);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(13)));
        Assert.False(subscription.IsEntitledAt(Now.AddDays(14)));
    }

    [Fact]
    public void Activate_RecordsProviderAndPaidPeriod()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);
        var periodEnd = Now.AddMonths(1);

        subscription.Activate(PaymentProvider.Stripe, "cus_123", "sub_123", periodEnd, Now);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(PaymentProvider.Stripe, subscription.Provider);
        Assert.Equal(periodEnd, subscription.CurrentPeriodEndsAt);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(20)));
    }

    [Fact]
    public void ExtendTrial_RejectsUnboundedExtensions()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);
        subscription.BeginTrial(PaymentProvider.Stripe, "cus_123", "sub_123", Now.AddDays(14), Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => subscription.ExtendTrial(366, Now));
    }
}
