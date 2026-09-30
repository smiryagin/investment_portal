using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Infrastructure.Payments;

namespace WiseLine.Portal.Api.Tests;

public sealed class StripeSubscriptionStateApplierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_Trialing_GrantsAccessUntilTrialEnd()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);

        StripeSubscriptionStateApplier.Apply(
            subscription,
            "trialing",
            "cus_test",
            "sub_test",
            Now.AddDays(14),
            Now.AddDays(14),
            false,
            Now);

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(13)));
        Assert.False(subscription.IsEntitledAt(Now.AddDays(15)));
    }

    [Fact]
    public void Apply_PastDue_RevokesAccess()
    {
        var subscription = CreateTrialing();

        StripeSubscriptionStateApplier.Apply(
            subscription,
            "past_due",
            "cus_test",
            "sub_test",
            Now.AddMonths(1),
            null,
            false,
            Now.AddDays(1));

        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.False(subscription.IsEntitledAt(Now.AddDays(1)));
    }

    [Fact]
    public void Apply_ScheduledCancellation_KeepsTrialAccess()
    {
        var subscription = CreateTrialing();

        StripeSubscriptionStateApplier.Apply(
            subscription,
            "trialing",
            "cus_test",
            "sub_test",
            Now.AddDays(14),
            Now.AddDays(14),
            true,
            Now.AddDays(1));

        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(2)));
    }

    [Fact]
    public void Apply_Canceled_RevokesAccess()
    {
        var subscription = CreateTrialing();

        StripeSubscriptionStateApplier.Apply(
            subscription,
            "canceled",
            "cus_test",
            "sub_test",
            Now.AddDays(14),
            null,
            false,
            Now.AddDays(1));

        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
        Assert.False(subscription.IsEntitledAt(Now.AddDays(1)));
    }

    [Fact]
    public void Apply_ResumedTrial_ClearsScheduledCancellation()
    {
        var subscription = CreateTrialing();
        subscription.ScheduleCancellation(Now.AddHours(1));

        StripeSubscriptionStateApplier.Apply(
            subscription,
            "trialing",
            "cus_test",
            "sub_test",
            Now.AddDays(14),
            Now.AddDays(14),
            false,
            Now.AddDays(1));

        Assert.False(subscription.CancelAtPeriodEnd);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(2)));
    }

    [Fact]
    public void Apply_Active_GrantsAccessForCurrentPeriod()
    {
        var subscription = CreateTrialing();

        StripeSubscriptionStateApplier.Apply(
            subscription,
            "active",
            "cus_test",
            "sub_test",
            Now.AddMonths(1),
            null,
            false,
            Now.AddDays(14));

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(15)));
    }

    private static Subscription CreateTrialing()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);
        subscription.BeginTrial(
            PaymentProvider.Stripe,
            "cus_test",
            "sub_test",
            Now.AddDays(14),
            Now);
        return subscription;
    }
}
