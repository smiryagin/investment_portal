namespace WiseLine.Portal.Domain.Integration;

public sealed class TradeEntitlementSyncRequest
{
    private TradeEntitlementSyncRequest()
    {
    }

    public TradeEntitlementSyncRequest(Guid portalUserId, DateTimeOffset now)
    {
        if (portalUserId == Guid.Empty)
        {
            throw new ArgumentException("A portal user is required.", nameof(portalUserId));
        }

        Id = Guid.NewGuid();
        PortalUserId = portalUserId;
        RequestedAt = now;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }

    public Guid PortalUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("An error is required.", nameof(error));
        }

        AttemptCount++;
        var trimmedError = error.Trim();
        LastError = trimmedError[..Math.Min(trimmedError.Length, 2000)];
        var delaySeconds = Math.Min(900, 15 * Math.Pow(2, Math.Min(AttemptCount - 1, 6)));
        NextAttemptAt = now.AddSeconds(delaySeconds);
    }
}
