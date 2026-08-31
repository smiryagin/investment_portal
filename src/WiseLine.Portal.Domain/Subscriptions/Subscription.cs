namespace WiseLine.Portal.Domain.Subscriptions;

public sealed class Subscription
{
    private Subscription()
    {
    }

    private Subscription(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        PlanKey = PlanCatalog.MonthlyPlanKey;
        Status = SubscriptionStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string PlanKey { get; private set; } = string.Empty;

    public PaymentProvider Provider { get; private set; }

    public string? ProviderCustomerId { get; private set; }

    public string? ProviderSubscriptionId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTimeOffset? TrialStartedAt { get; private set; }

    public DateTimeOffset? TrialEndsAt { get; private set; }

    public DateTimeOffset? CurrentPeriodEndsAt { get; private set; }

    public DateTimeOffset? CanceledAt { get; private set; }

    public bool CancelAtPeriodEnd { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Subscription CreatePending(Guid userId, DateTimeOffset now) => new(userId, now);

    public bool IsEntitledAt(DateTimeOffset now) => Status switch
    {
        SubscriptionStatus.Trialing => TrialEndsAt is { } trialEnd && now < trialEnd,
        SubscriptionStatus.Active => CurrentPeriodEndsAt is null || now < CurrentPeriodEndsAt,
        _ => false
    };

    public void ExtendTrial(int days, DateTimeOffset now)
    {
        if (days is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Trial extension must be between 1 and 365 days.");
        }

        if (Status != SubscriptionStatus.Trialing)
        {
            throw new InvalidOperationException("Only a trialing subscription can be extended.");
        }

        if (TrialEndsAt is null)
        {
            throw new InvalidOperationException("The trial has not started.");
        }

        TrialEndsAt = TrialEndsAt.Value.AddDays(days);
        UpdatedAt = now;
    }

    public void BeginTrial(
        PaymentProvider provider,
        string providerCustomerId,
        string providerSubscriptionId,
        DateTimeOffset trialEndsAt,
        DateTimeOffset now)
    {
        if (provider == PaymentProvider.None)
        {
            throw new ArgumentException("A payment provider is required.", nameof(provider));
        }

        if (trialEndsAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(trialEndsAt), "The trial must end in the future.");
        }

        Provider = provider;
        ProviderCustomerId = RequireValue(providerCustomerId, nameof(providerCustomerId));
        ProviderSubscriptionId = RequireValue(providerSubscriptionId, nameof(providerSubscriptionId));
        TrialStartedAt ??= now;
        TrialEndsAt = trialEndsAt;
        CurrentPeriodEndsAt = trialEndsAt;
        Status = SubscriptionStatus.Trialing;
        UpdatedAt = now;
    }

    public void Activate(
        PaymentProvider provider,
        string providerCustomerId,
        string providerSubscriptionId,
        DateTimeOffset periodEndsAt,
        DateTimeOffset now)
    {
        if (provider == PaymentProvider.None)
        {
            throw new ArgumentException("A payment provider is required.", nameof(provider));
        }

        if (periodEndsAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(periodEndsAt), "The billing period must end in the future.");
        }

        Provider = provider;
        ProviderCustomerId = RequireValue(providerCustomerId, nameof(providerCustomerId));
        ProviderSubscriptionId = RequireValue(providerSubscriptionId, nameof(providerSubscriptionId));
        CurrentPeriodEndsAt = periodEndsAt;
        Status = SubscriptionStatus.Active;
        CancelAtPeriodEnd = false;
        CanceledAt = null;
        UpdatedAt = now;
    }

    public void MarkPastDue(DateTimeOffset now)
    {
        Status = SubscriptionStatus.PastDue;
        UpdatedAt = now;
    }

    public void ScheduleCancellation(DateTimeOffset now)
    {
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
        {
            return;
        }

        CancelAtPeriodEnd = true;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Canceled;
        CancelAtPeriodEnd = false;
        CanceledAt = now;
        UpdatedAt = now;
    }

    public void Expire(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Expired;
        UpdatedAt = now;
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return value.Trim();
    }
}
