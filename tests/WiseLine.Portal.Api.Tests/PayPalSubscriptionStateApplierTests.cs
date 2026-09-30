using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Infrastructure.Payments;

namespace WiseLine.Portal.Api.Tests;

public sealed class PayPalSubscriptionStateApplierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_ActivatedPendingSubscription_StartsTrial()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);

        PayPalSubscriptionStateApplier.Apply(
            subscription,
            PayPalSubscriptionStateApplier.SubscriptionActivated,
            "ACTIVE",
            "payer_test",
            "I-TEST",
            Now.AddDays(14),
            Now);

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Equal(Now.AddDays(14), subscription.TrialEndsAt);
        Assert.True(subscription.IsEntitledAt(Now.AddDays(13)));
    }

    [Fact]
    public void Apply_CompletedSale_ActivatesSubscriptionAndAdvancesPeriod()
    {
        var subscription = CreateTrialing();
        var paymentTime = Now.AddDays(14);

        PayPalSubscriptionStateApplier.Apply(
            subscription,
            PayPalSubscriptionStateApplier.PaymentCompleted,
            "ACTIVE",
            "payer_test",
            "I-TEST",
            paymentTime.AddMonths(1),
            paymentTime);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(paymentTime.AddMonths(1), subscription.CurrentPeriodEndsAt);
        Assert.True(subscription.IsEntitledAt(paymentTime.AddDays(1)));
    }

    [Fact]
    public void Apply_UpdatedActiveDuringTrial_PreservesTrialState()
    {
        var subscription = CreateTrialing();

        PayPalSubscriptionStateApplier.Apply(
            subscription,
            PayPalSubscriptionStateApplier.SubscriptionUpdated,
            "ACTIVE",
            "payer_test",
            "I-TEST",
            Now.AddDays(14),
            Now.AddDays(1));

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Equal(Now.AddDays(14), subscription.TrialEndsAt);
    }

    [Fact]
    public void Apply_UpdatedActiveAfterTrial_RecoversActiveState()
    {
        var subscription = CreateTrialing();
        var updateTime = Now.AddDays(15);

        PayPalSubscriptionStateApplier.Apply(
            subscription,
            PayPalSubscriptionStateApplier.SubscriptionUpdated,
            "ACTIVE",
            "payer_test",
            "I-TEST",
            updateTime.AddMonths(1),
            updateTime);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.IsEntitledAt(updateTime.AddDays(1)));
    }

    [Theory]
    [InlineData(PayPalSubscriptionStateApplier.SubscriptionSuspended)]
    [InlineData(PayPalSubscriptionStateApplier.SubscriptionPaymentFailed)]
    public void Apply_ProviderFailure_RevokesAccess(string eventType)
    {
        var subscription = CreateTrialing();

        PayPalSubscriptionStateApplier.Apply(
            subscription,
            eventType,
            "SUSPENDED",
            "payer_test",
            "I-TEST",
            Now.AddDays(14),
            Now.AddDays(1));

        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.False(subscription.IsEntitledAt(Now.AddDays(1)));
    }

    [Fact]
    public void Apply_Cancelled_RevokesAccess()
    {
        var subscription = CreateTrialing();

        PayPalSubscriptionStateApplier.Apply(
            subscription,
            PayPalSubscriptionStateApplier.SubscriptionCancelled,
            "CANCELLED",
            "payer_test",
            "I-TEST",
            Now.AddDays(14),
            Now.AddDays(1));

        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
        Assert.False(subscription.IsEntitledAt(Now.AddDays(1)));
    }

    private static Subscription CreateTrialing()
    {
        var subscription = Subscription.CreatePending(Guid.NewGuid(), Now);
        subscription.BeginTrial(
            PaymentProvider.PayPal,
            "payer_test",
            "I-TEST",
            Now.AddDays(14),
            Now);
        return subscription;
    }
}
