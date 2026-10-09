using System.Text.Json;

namespace WiseLine.Portal.Application.Trade;

public sealed record PortfolioSummary(
    Guid Id,
    string Name,
    string? StrategyName,
    decimal MarketValue,
    decimal DayChange,
    decimal DayChangePercent,
    int PositionCount,
    DateTimeOffset? UpdatedAt);

public sealed record PortfolioDetails(
    Guid Id,
    string Name,
    string? Description,
    string? StrategyName,
    decimal MarketValue,
    decimal TotalCost,
    decimal UnrealizedGain,
    decimal UnrealizedGainPercent,
    IReadOnlyList<PortfolioPosition> Positions,
    IReadOnlyList<PortfolioStrategy> Strategies);

public sealed record PortfolioPosition(
    string Id,
    string Symbol,
    string? Description,
    decimal Quantity,
    decimal? AveragePrice,
    decimal? CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedGain,
    decimal UnrealizedGainPercent);

public sealed record PortfolioStrategy(
    Guid Id,
    Guid? AccountId,
    string Name,
    string Type,
    JsonElement Rule,
    string Scope,
    DateTimeOffset? UpdatedAt);
