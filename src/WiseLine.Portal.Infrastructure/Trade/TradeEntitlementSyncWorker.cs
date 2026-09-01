using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Trade;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Trade;

public sealed class TradeEntitlementSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<TradeDatabaseOptions> options,
    TimeProvider timeProvider,
    ILogger<TradeEntitlementSyncWorker> logger) : BackgroundService
{
    private readonly TradeDatabaseOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var pollDelay = TimeSpan.FromSeconds(Math.Clamp(_options.EntitlementSyncPollSeconds, 5, 300));
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
        var tradeGateway = scope.ServiceProvider.GetRequiredService<ITradePortalGateway>();
        var now = timeProvider.GetUtcNow();
        var request = await dbContext.TradeEntitlementSyncRequests
            .Where(x => x.ProcessedAt == null && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return false;
        }

        try
        {
            await tradeGateway.SynchronizeEntitlementAsync(request.PortalUserId, cancellationToken);
            request.MarkProcessed(timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            request.MarkFailed(exception.Message, timeProvider.GetUtcNow());
            logger.LogWarning(
                exception,
                "Trade entitlement synchronization failed for portal user {PortalUserId}; attempt {AttemptCount} will be retried.",
                request.PortalUserId,
                request.AttemptCount);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
