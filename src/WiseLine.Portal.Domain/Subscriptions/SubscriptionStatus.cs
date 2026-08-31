namespace WiseLine.Portal.Domain.Subscriptions;

public enum SubscriptionStatus
{
    Pending = 1,
    Trialing = 2,
    Active = 3,
    PastDue = 4,
    Canceled = 5,
    Expired = 6
}
