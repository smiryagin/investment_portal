namespace WiseLine.Portal.Application.Trade;

public sealed record PortfolioSummary(
    long Id,
    string Name,
    string? StrategyName,
    decimal MarketValue,
    decimal DayChange,
    decimal DayChangePercent,
    int PositionCount,
    DateTimeOffset? UpdatedAt);

public sealed record PortfolioDetails(
    long Id,
    string Name,
    string? Description,
    string? StrategyName,
    decimal MarketValue,
    decimal TotalCost,
    decimal UnrealizedGain,
    decimal UnrealizedGainPercent,
    IReadOnlyList<PortfolioPosition> Positions);

public sealed record PortfolioPosition(
    long Id,
    string Symbol,
    string? Description,
    decimal Quantity,
    decimal? AveragePrice,
    decimal? CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedGain,
    decimal UnrealizedGainPercent);
