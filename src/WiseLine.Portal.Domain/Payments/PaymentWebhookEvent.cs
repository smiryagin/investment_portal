namespace WiseLine.Portal.Domain.Payments;

public sealed class PaymentWebhookEvent
{
    private PaymentWebhookEvent()
    {
    }

    public PaymentWebhookEvent(
        string provider,
        string providerEventId,
        string eventType,
        string payloadSha256,
        DateTimeOffset receivedAt)
    {
        Id = Guid.NewGuid();
        Provider = provider;
        ProviderEventId = providerEventId;
        EventType = eventType;
        PayloadSha256 = payloadSha256;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderEventId { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string PayloadSha256 { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? ProcessingError { get; private set; }

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        ProcessingError = null;
    }

    public void MarkFailed(string error) => ProcessingError = error;
}
