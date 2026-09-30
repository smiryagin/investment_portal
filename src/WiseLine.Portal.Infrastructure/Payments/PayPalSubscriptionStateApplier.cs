using WiseLine.Portal.Domain.Subscriptions;

namespace WiseLine.Portal.Infrastructure.Payments;

internal static class PayPalSubscriptionStateApplier
{
    internal const string SubscriptionActivated = "BILLING.SUBSCRIPTION.ACTIVATED";
    internal const string SubscriptionUpdated = "BILLING.SUBSCRIPTION.UPDATED";
    internal const string SubscriptionSuspended = "BILLING.SUBSCRIPTION.SUSPENDED";
    internal const string SubscriptionPaymentFailed = "BILLING.SUBSCRIPTION.PAYMENT.FAILED";
    internal const string SubscriptionCancelled = "BILLING.SUBSCRIPTION.CANCELLED";
    internal const string SubscriptionExpired = "BILLING.SUBSCRIPTION.EXPIRED";
    internal const string PaymentCompleted = "PAYMENT.SALE.COMPLETED";

    public static void Apply(
        Subscription subscription,
        string eventType,
        string? providerStatus,
        string providerCustomerId,
        string providerSubscriptionId,
        DateTimeOffset nextBilling,
        DateTimeOffset now)
    {
        switch (eventType)
        {
            case SubscriptionActivated:
                ApplyActiveState(
                    subscription,
                    providerCustomerId,
                    providerSubscriptionId,
                    nextBilling,
                    now);
                break;
            case SubscriptionUpdated:
                ApplyUpdatedState(
                    subscription,
                    providerStatus,
                    providerCustomerId,
                    providerSubscriptionId,
                    nextBilling,
                    now);
                break;
            case PaymentCompleted:
                subscription.Activate(
                    PaymentProvider.PayPal,
                    providerCustomerId,
                    providerSubscriptionId,
                    nextBilling,
                    now);
                break;
            case SubscriptionSuspended:
            case SubscriptionPaymentFailed:
                subscription.MarkPastDue(now);
                break;
            case SubscriptionCancelled:
                subscription.Cancel(now);
                break;
            case SubscriptionExpired:
                subscription.Expire(now);
                break;
        }
    }

    private static void ApplyActiveState(
        Subscription subscription,
        string providerCustomerId,
        string providerSubscriptionId,
        DateTimeOffset nextBilling,
        DateTimeOffset now)
    {
        if (subscription.Status == SubscriptionStatus.Pending)
        {
            subscription.BeginTrial(
                PaymentProvider.PayPal,
                providerCustomerId,
                providerSubscriptionId,
                nextBilling,
                now);
            return;
        }

        subscription.Activate(
            PaymentProvider.PayPal,
            providerCustomerId,
            providerSubscriptionId,
            nextBilling,
            now);
    }

    private static void ApplyUpdatedState(
        Subscription subscription,
        string? providerStatus,
        string providerCustomerId,
        string providerSubscriptionId,
        DateTimeOffset nextBilling,
        DateTimeOffset now)
    {
        switch (providerStatus?.Trim().ToUpperInvariant())
        {
            case "ACTIVE":
                if (subscription.Status == SubscriptionStatus.Pending)
                {
                    subscription.BeginTrial(
                        PaymentProvider.PayPal,
                        providerCustomerId,
                        providerSubscriptionId,
                        nextBilling,
                        now);
                }
                else if (subscription.Status == SubscriptionStatus.Trialing &&
                         subscription.TrialEndsAt is { } trialEnd &&
                         now < trialEnd)
                {
                    subscription.BeginTrial(
                        PaymentProvider.PayPal,
                        providerCustomerId,
                        providerSubscriptionId,
                        nextBilling,
                        now);
                }
                else
                {
                    subscription.Activate(
                        PaymentProvider.PayPal,
                        providerCustomerId,
                        providerSubscriptionId,
                        nextBilling,
                        now);
                }
                break;
            case "SUSPENDED":
                subscription.MarkPastDue(now);
                break;
            case "CANCELLED":
                subscription.Cancel(now);
                break;
            case "EXPIRED":
                subscription.Expire(now);
                break;
        }
    }
}
