using WiseLine.Portal.Domain.Integration;

namespace WiseLine.Portal.Domain.Tests;

public sealed class TradeEntitlementSyncRequestTests
{
    [Fact]
    public void MarkFailedSchedulesAnIncreasingRetryAndCapsStoredError()
    {
        var now = DateTimeOffset.Parse("2026-09-01T12:00:00Z");
        var request = new TradeEntitlementSyncRequest(Guid.NewGuid(), now);

        request.MarkFailed(new string('x', 2100), now);

        Assert.Equal(1, request.AttemptCount);
        Assert.Equal(now.AddSeconds(15), request.NextAttemptAt);
        Assert.Equal(2000, request.LastError?.Length);
        Assert.Null(request.ProcessedAt);
    }

    [Fact]
    public void MarkProcessedClearsTheLastError()
    {
        var now = DateTimeOffset.Parse("2026-09-01T12:00:00Z");
        var request = new TradeEntitlementSyncRequest(Guid.NewGuid(), now);
        request.MarkFailed("temporary outage", now);

        request.MarkProcessed(now.AddMinutes(1));

        Assert.Equal(now.AddMinutes(1), request.ProcessedAt);
        Assert.Null(request.LastError);
    }
}
