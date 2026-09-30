using WiseLine.Portal.Domain.Subscriptions;

namespace WiseLine.Portal.Infrastructure.Payments;

internal static class StripeSubscriptionStateApplier
{
    public static void Apply(
        Subscription subscription,
        string status,
        string customerId,
        string providerSubscriptionId,
        DateTimeOffset periodEnd,
        DateTimeOffset? trialEnd,
        bool cancelAtPeriodEnd,
        DateTimeOffset now)
    {
        switch (status)
        {
            case "trialing" when trialEnd is { } end && end > now:
                subscription.BeginTrial(
                    PaymentProvider.Stripe,
                    customerId,
                    providerSubscriptionId,
                    end,
                    now);
                break;
            case "active":
                subscription.Activate(
                    PaymentProvider.Stripe,
                    customerId,
                    providerSubscriptionId,
                    periodEnd,
                    now);
                break;
            case "past_due":
            case "unpaid":
                subscription.MarkPastDue(now);
                break;
            case "canceled":
                subscription.Cancel(now);
                break;
            case "incomplete_expired":
                subscription.Expire(now);
                break;
        }

        if (cancelAtPeriodEnd)
        {
            subscription.ScheduleCancellation(now);
        }
        else
        {
            subscription.ClearScheduledCancellation(now);
        }
    }
}
