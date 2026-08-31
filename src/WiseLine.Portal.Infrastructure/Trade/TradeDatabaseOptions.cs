namespace WiseLine.Portal.Infrastructure.Trade;

public sealed class TradeDatabaseOptions
{
    public const string SectionName = "TradeDatabase";

    public bool Enabled { get; init; }

    public string GetPortfoliosProcedure { get; init; } = "invest.Portal_GetPortfolios";

    public string GetPortfolioProcedure { get; init; } = "invest.Portal_GetPortfolio";

    public string GetMcpTokensProcedure { get; init; } = "invest.Portal_GetMcpTokens";

    public string CreateMcpTokenProcedure { get; init; } = "invest.Portal_CreateMcpToken";

    public string RevokeMcpTokenProcedure { get; init; } = "invest.Portal_RevokeMcpToken";
}
