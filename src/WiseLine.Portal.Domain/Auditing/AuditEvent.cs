namespace WiseLine.Portal.Domain.Auditing;

public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid? userId,
        string eventType,
        string outcome,
        string? ipAddress,
        string? metadataJson,
        DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        EventType = eventType;
        Outcome = outcome;
        IpAddress = ipAddress;
        MetadataJson = metadataJson;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Outcome { get; private set; } = string.Empty;

    public string? IpAddress { get; private set; }

    public string? MetadataJson { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
