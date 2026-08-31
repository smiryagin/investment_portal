using WiseLine.Portal.Domain.Subscriptions;

namespace WiseLine.Portal.Domain.Tests;

public sealed class PromotionCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Code_IsNormalizedAndHonorsRedemptionLimit()
    {
        var code = new PromotionCode(" launch-14 ", 14, 1, Now.AddDays(-1), Now.AddDays(7), Now);

        Assert.Equal("LAUNCH-14", code.Code);
        Assert.True(code.CanRedeemAt(Now));

        code.RecordRedemption(Now);

        Assert.False(code.CanRedeemAt(Now));
    }
}
