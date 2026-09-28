using WiseLine.Portal.Domain.Email;

namespace WiseLine.Portal.Domain.Tests;

public sealed class EmailOutboxMessageTests
{
    [Fact]
    public void Constructor_CreatesPendingIdempotentMessage()
    {
        var now = DateTimeOffset.Parse("2026-09-28T20:00:00Z");

        var message = CreateMessage(now);

        Assert.Equal(EmailDeliveryStatus.Pending, message.Status);
        Assert.Equal(now, message.NextAttemptAt);
        Assert.Equal($"wlp-email/{message.Id:N}", message.IdempotencyKey);
        Assert.Equal(0, message.AttemptCount);
    }

    [Fact]
    public void MarkSent_RecordsProviderIdAndScrubsSensitiveContent()
    {
        var now = DateTimeOffset.Parse("2026-09-28T20:00:00Z");
        var message = CreateMessage(now);

        message.MarkSent("provider-message", now.AddSeconds(1));

        Assert.Equal(EmailDeliveryStatus.Sent, message.Status);
        Assert.Equal("provider-message", message.ProviderMessageId);
        Assert.Equal(string.Empty, message.HtmlBody);
        Assert.Equal(string.Empty, message.TextBody);
    }

    [Fact]
    public void RetryableFailure_SchedulesRetryAndKeepsContent()
    {
        var now = DateTimeOffset.Parse("2026-09-28T20:00:00Z");
        var message = CreateMessage(now);

        message.MarkSendFailed("Temporary failure", retryable: true, now);

        Assert.Equal(EmailDeliveryStatus.Pending, message.Status);
        Assert.True(message.NextAttemptAt > now);
        Assert.NotEmpty(message.HtmlBody);
        Assert.Equal(1, message.AttemptCount);
    }

    [Fact]
    public void PermanentFailure_StopsRetryAndScrubsSensitiveContent()
    {
        var now = DateTimeOffset.Parse("2026-09-28T20:00:00Z");
        var message = CreateMessage(now);

        message.MarkSendFailed("Invalid recipient", retryable: false, now);

        Assert.Equal(EmailDeliveryStatus.Failed, message.Status);
        Assert.Equal(now, message.FailedAt);
        Assert.Equal(string.Empty, message.HtmlBody);
        Assert.Equal(string.Empty, message.TextBody);
    }

    [Fact]
    public void DeliveredWebhook_UpdatesDeliveryState()
    {
        var now = DateTimeOffset.Parse("2026-09-28T20:00:00Z");
        var message = CreateMessage(now);
        message.MarkSent("provider-message", now);

        message.ApplyProviderEvent("email.delivered", now.AddMinutes(1));

        Assert.Equal(EmailDeliveryStatus.Delivered, message.Status);
        Assert.Equal(now.AddMinutes(1), message.DeliveredAt);
    }

    private static EmailOutboxMessage CreateMessage(DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            "USER@example.com",
            "password-reset",
            "Reset password",
            "<html>secret link</html>",
            "secret link",
            now);
}
