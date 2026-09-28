using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Email;
using WiseLine.Portal.Domain.Email;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Email;

public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private readonly EmailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var pollDelay = TimeSpan.FromSeconds(Math.Clamp(_options.OutboxPollSeconds, 2, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await ProcessOneAsync(stoppingToken);
            if (!processed)
            {
                await Task.Delay(pollDelay, timeProvider, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var now = timeProvider.GetUtcNow();
        var candidateId = await dbContext.EmailOutboxMessages
            .Where(message =>
                message.NextAttemptAt <= now &&
                (message.Status == EmailDeliveryStatus.Pending ||
                 (message.Status == EmailDeliveryStatus.Processing && message.LockedUntil <= now)))
            .OrderBy(message => message.NextAttemptAt)
            .ThenBy(message => message.RequestedAt)
            .Select(message => (Guid?)message.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (candidateId is null)
        {
            return false;
        }

        var processingToken = Guid.NewGuid();
        var lockedUntil = now.AddMinutes(5);
        var claimed = await dbContext.EmailOutboxMessages
            .Where(message =>
                message.Id == candidateId &&
                message.NextAttemptAt <= now &&
                (message.Status == EmailDeliveryStatus.Pending ||
                 (message.Status == EmailDeliveryStatus.Processing && message.LockedUntil <= now)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.Status, EmailDeliveryStatus.Processing)
                    .SetProperty(message => message.ProcessingToken, processingToken)
                    .SetProperty(message => message.LockedUntil, lockedUntil),
                cancellationToken);

        if (claimed != 1)
        {
            return true;
        }

        var outboxMessage = await dbContext.EmailOutboxMessages
            .SingleAsync(message => message.ProcessingToken == processingToken, cancellationToken);

        try
        {
            var providerMessageId = await sender.SendAsync(
                new EmailSendRequest(
                    outboxMessage.ToAddress,
                    outboxMessage.Subject,
                    outboxMessage.HtmlBody,
                    outboxMessage.TextBody,
                    outboxMessage.IdempotencyKey),
                cancellationToken);
            outboxMessage.MarkSent(providerMessageId, timeProvider.GetUtcNow());

            var earlyProviderEvents = await dbContext.EmailWebhookEvents
                .AsNoTracking()
                .Where(webhookEvent => webhookEvent.ProviderMessageId == providerMessageId)
                .OrderBy(webhookEvent => webhookEvent.OccurredAt)
                .ToListAsync(cancellationToken);
            foreach (var providerEvent in earlyProviderEvents)
            {
                outboxMessage.ApplyProviderEvent(providerEvent.EventType, providerEvent.OccurredAt);
            }
        }
        catch (EmailSendException exception)
        {
            outboxMessage.MarkSendFailed(exception.Message, exception.Retryable, timeProvider.GetUtcNow());
            logger.LogWarning(
                exception,
                "Transactional email {EmailOutboxId} failed on attempt {AttemptCount}; retryable: {Retryable}.",
                outboxMessage.Id,
                outboxMessage.AttemptCount,
                exception.Retryable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            outboxMessage.MarkSendFailed("An unexpected email delivery error occurred.", retryable: true, timeProvider.GetUtcNow());
            logger.LogError(exception, "Unexpected transactional email failure for outbox item {EmailOutboxId}.", outboxMessage.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
