using WiseLine.Portal.Domain.Users;

namespace WiseLine.Portal.Domain.Tests;

public sealed class InvestmentIdentityLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_StoresGuidTradeIdentity()
    {
        var portalUserId = Guid.NewGuid();
        var tradeUserId = Guid.NewGuid();

        var link = new InvestmentIdentityLink(portalUserId, tradeUserId, Now);

        Assert.Equal(portalUserId, link.PortalUserId);
        Assert.Equal(tradeUserId, link.TradeUserId);
    }

    [Fact]
    public void Constructor_RejectsEmptyIdentifiers()
    {
        Assert.Throws<ArgumentException>(() =>
            new InvestmentIdentityLink(Guid.Empty, Guid.NewGuid(), Now));
        Assert.Throws<ArgumentException>(() =>
            new InvestmentIdentityLink(Guid.NewGuid(), Guid.Empty, Now));
    }
}
