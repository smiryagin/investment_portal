using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WiseLine.Portal.Application.Email;
using WiseLine.Portal.Domain.Email;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Email;

public sealed class ResendWebhookProcessor(
    PortalDbContext dbContext,
    TimeProvider timeProvider) : IEmailWebhookProcessor
{
    public async Task ProcessAsync(
        string providerEventId,
        string payload,
        CancellationToken cancellationToken)
    {
        if (await dbContext.EmailWebhookEvents.AnyAsync(
                webhookEvent => webhookEvent.ProviderEventId == providerEventId,
                cancellationToken))
        {
            return;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventType = root.GetProperty("type").GetString();
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new JsonException("The webhook event type is missing.");
        }

        var occurredAt = root.TryGetProperty("created_at", out var createdAtProperty) &&
                         createdAtProperty.ValueKind == JsonValueKind.String &&
                         DateTimeOffset.TryParse(createdAtProperty.GetString(), out var parsedOccurredAt)
            ? parsedOccurredAt
            : timeProvider.GetUtcNow();

        string? providerMessageId = null;
        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("email_id", out var emailIdProperty) &&
            emailIdProperty.ValueKind == JsonValueKind.String)
        {
            providerMessageId = emailIdProperty.GetString();
        }

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        dbContext.EmailWebhookEvents.Add(new EmailWebhookEvent(
            providerEventId,
            eventType,
            providerMessageId,
            payloadHash,
            occurredAt,
            timeProvider.GetUtcNow()));

        if (!string.IsNullOrWhiteSpace(providerMessageId))
        {
            var outboxMessage = await dbContext.EmailOutboxMessages
                .SingleOrDefaultAsync(
                    message => message.ProviderMessageId == providerMessageId,
                    cancellationToken);
            outboxMessage?.ApplyProviderEvent(eventType, occurredAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
