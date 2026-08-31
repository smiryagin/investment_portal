namespace WiseLine.Portal.Domain.Users;

public sealed class InvestmentIdentityLink
{
    private InvestmentIdentityLink()
    {
    }

    public InvestmentIdentityLink(Guid portalUserId, long tradeUserId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        PortalUserId = portalUserId;
        TradeUserId = tradeUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid PortalUserId { get; private set; }

    public long TradeUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
