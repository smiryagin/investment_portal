namespace WiseLine.Portal.Domain.Email;

public sealed class EmailWebhookEvent
{
    private EmailWebhookEvent()
    {
    }

    public EmailWebhookEvent(
        string providerEventId,
        string eventType,
        string? providerMessageId,
        string payloadSha256,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);

        Id = Guid.NewGuid();
        ProviderEventId = providerEventId.Trim();
        EventType = eventType.Trim();
        ProviderMessageId = string.IsNullOrWhiteSpace(providerMessageId) ? null : providerMessageId.Trim();
        PayloadSha256 = payloadSha256.Trim();
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; private set; }

    public string ProviderEventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string? ProviderMessageId { get; private set; }

    public string PayloadSha256 { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }
}
