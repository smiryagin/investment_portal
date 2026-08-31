namespace WiseLine.Portal.Application.Subscriptions;

public sealed record SubscriptionSnapshot(
    string PlanKey,
    string Status,
    bool IsEntitled,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEndsAt,
    bool CancelAtPeriodEnd,
    string? PaymentProvider);
