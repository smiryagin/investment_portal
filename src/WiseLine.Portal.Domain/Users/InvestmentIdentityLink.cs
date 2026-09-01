namespace WiseLine.Portal.Domain.Users;

public sealed class InvestmentIdentityLink
{
    private InvestmentIdentityLink()
    {
    }

    public InvestmentIdentityLink(Guid portalUserId, Guid tradeUserId, DateTimeOffset now)
    {
        if (portalUserId == Guid.Empty)
        {
            throw new ArgumentException("A portal user is required.", nameof(portalUserId));
        }

        if (tradeUserId == Guid.Empty)
        {
            throw new ArgumentException("A Trade user is required.", nameof(tradeUserId));
        }

        Id = Guid.NewGuid();
        PortalUserId = portalUserId;
        TradeUserId = tradeUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid PortalUserId { get; private set; }

    public Guid TradeUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
