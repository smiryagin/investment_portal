using System.Security.Claims;
using WiseLine.Portal.Api.OAuth;

namespace WiseLine.Portal.Api.Tests;

public sealed class OAuthAuthorizationSessionTests
{
    [Fact]
    public void StartedAt_RoundTripsThroughPrivateClaim()
    {
        var expected = new DateTimeOffset(2026, 10, 7, 12, 30, 45, TimeSpan.Zero);
        var identity = new ClaimsIdentity("test");
        OAuthAuthorizationSession.SetStartedAt(identity, expected);

        var found = OAuthAuthorizationSession.TryGetStartedAt(
            new ClaimsPrincipal(identity),
            out var actual);

        Assert.True(found);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MissingStartedAt_IsRejected()
    {
        var found = OAuthAuthorizationSession.TryGetStartedAt(
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            out _);

        Assert.False(found);
    }

    [Theory]
    [InlineData(89, false)]
    [InlineData(90, true)]
    [InlineData(91, true)]
    public void AbsoluteLifetime_ExpiresAtNinetyDays(int elapsedDays, bool expected)
    {
        var startedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(
            expected,
            OAuthAuthorizationSession.IsExpired(startedAt, startedAt.AddDays(elapsedDays), 90));
    }
}
