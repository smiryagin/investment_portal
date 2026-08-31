namespace WiseLine.Portal.Domain.Subscriptions;

public sealed class PromotionCode
{
    private PromotionCode()
    {
    }

    public PromotionCode(
        string code,
        int trialExtensionDays,
        int? maximumRedemptions,
        DateTimeOffset startsAt,
        DateTimeOffset? expiresAt,
        DateTimeOffset now)
    {
        if (trialExtensionDays is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(trialExtensionDays));
        }

        if (maximumRedemptions is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRedemptions));
        }

        if (expiresAt <= startsAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt));
        }

        Id = Guid.NewGuid();
        Code = Normalize(code);
        TrialExtensionDays = trialExtensionDays;
        MaximumRedemptions = maximumRedemptions;
        StartsAt = startsAt;
        ExpiresAt = expiresAt;
        IsEnabled = true;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public int TrialExtensionDays { get; private set; }

    public int? MaximumRedemptions { get; private set; }

    public int RedemptionCount { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool CanRedeemAt(DateTimeOffset now) =>
        IsEnabled &&
        now >= StartsAt &&
        (ExpiresAt is null || now < ExpiresAt) &&
        (MaximumRedemptions is null || RedemptionCount < MaximumRedemptions);

    public void RecordRedemption(DateTimeOffset now)
    {
        if (!CanRedeemAt(now))
        {
            throw new InvalidOperationException("Promotion code is not redeemable.");
        }

        RedemptionCount++;
    }

    public void Disable() => IsEnabled = false;

    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A promotion code is required.", nameof(code));
        }

        return code.Trim().ToUpperInvariant();
    }
}
