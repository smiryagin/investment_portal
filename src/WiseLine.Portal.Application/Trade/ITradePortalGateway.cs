namespace WiseLine.Portal.Application.Trade;

public interface ITradePortalGateway
{
    Task<IReadOnlyList<PortfolioSummary>> GetPortfoliosAsync(
        Guid portalUserId,
        CancellationToken cancellationToken = default);

    Task<PortfolioDetails?> GetPortfolioAsync(
        Guid portalUserId,
        long portfolioId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpTokenSummary>> GetMcpTokensAsync(
        Guid portalUserId,
        CancellationToken cancellationToken = default);

    Task<McpTokenCreated> CreateMcpTokenAsync(
        Guid portalUserId,
        string displayName,
        CancellationToken cancellationToken = default);

    Task RevokeMcpTokenAsync(
        Guid portalUserId,
        long tokenId,
        CancellationToken cancellationToken = default);
}
