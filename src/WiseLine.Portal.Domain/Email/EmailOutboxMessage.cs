namespace WiseLine.Portal.Domain.Email;

public sealed class EmailOutboxMessage
{
    public const int MaximumAttempts = 8;

    private EmailOutboxMessage()
    {
    }

    public EmailOutboxMessage(
        Guid? userId,
        string toAddress,
        string templateKey,
        string subject,
        string htmlBody,
        string textBody,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty user identifier is required when supplied.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(textBody);

        Id = Guid.NewGuid();
        UserId = userId;
        ToAddress = toAddress.Trim().ToLowerInvariant();
        TemplateKey = templateKey.Trim();
        Subject = subject.Trim();
        HtmlBody = htmlBody;
        TextBody = textBody;
        IdempotencyKey = $"wlp-email/{Id:N}";
        RequestedAt = now;
        NextAttemptAt = now;
        Status = EmailDeliveryStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string ToAddress { get; private set; } = string.Empty;

    public string TemplateKey { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string HtmlBody { get; private set; } = string.Empty;

    public string TextBody { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public EmailDeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public Guid? ProcessingToken { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? LastError { get; private set; }

    public void MarkSent(string providerMessageId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);

        AttemptCount++;
        ProviderMessageId = providerMessageId.Trim();
        SentAt = now;
        Status = EmailDeliveryStatus.Sent;
        LastError = null;
        ScrubContent();
        ClearProcessingLock();
    }

    public void MarkSendFailed(string error, bool retryable, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        AttemptCount++;
        LastError = TrimError(error);
        ClearProcessingLock();

        if (retryable && AttemptCount < MaximumAttempts)
        {
            var delaySeconds = Math.Min(900, 15 * Math.Pow(2, Math.Min(AttemptCount - 1, 6)));
            NextAttemptAt = now.AddSeconds(delaySeconds);
            Status = EmailDeliveryStatus.Pending;
            return;
        }

        Status = EmailDeliveryStatus.Failed;
        FailedAt = now;
        ScrubContent();
    }

    public void ApplyProviderEvent(string eventType, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        switch (eventType.Trim().ToLowerInvariant())
        {
            case "email.sent":
                if (Status is EmailDeliveryStatus.Pending or EmailDeliveryStatus.Processing)
                {
                    Status = EmailDeliveryStatus.Sent;
                    SentAt ??= occurredAt;
                }
                break;
            case "email.delivered":
                Status = EmailDeliveryStatus.Delivered;
                DeliveredAt = occurredAt;
                LastError = null;
                break;
            case "email.delivery_delayed":
                Status = EmailDeliveryStatus.Delayed;
                LastError = "The receiving mail server delayed delivery.";
                break;
            case "email.bounced":
                MarkProviderFailure(EmailDeliveryStatus.Bounced, "The message bounced.", occurredAt);
                break;
            case "email.complained":
                MarkProviderFailure(EmailDeliveryStatus.Complained, "The recipient reported the message as spam.", occurredAt);
                break;
            case "email.suppressed":
                MarkProviderFailure(EmailDeliveryStatus.Suppressed, "The provider suppressed the recipient address.", occurredAt);
                break;
            case "email.failed":
                MarkProviderFailure(EmailDeliveryStatus.Failed, "The provider could not deliver the message.", occurredAt);
                break;
        }
    }

    private void MarkProviderFailure(EmailDeliveryStatus status, string error, DateTimeOffset occurredAt)
    {
        Status = status;
        LastError = error;
        FailedAt = occurredAt;
    }

    private void ClearProcessingLock()
    {
        ProcessingToken = null;
        LockedUntil = null;
    }

    private void ScrubContent()
    {
        HtmlBody = string.Empty;
        TextBody = string.Empty;
    }

    private static string TrimError(string error)
    {
        var trimmed = error.Trim();
        return trimmed[..Math.Min(trimmed.Length, 2000)];
    }
}
