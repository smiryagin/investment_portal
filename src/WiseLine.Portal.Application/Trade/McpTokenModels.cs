namespace WiseLine.Portal.Application.Trade;

public sealed record McpTokenSummary(
    long Id,
    string DisplayName,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked);

public sealed record McpTokenCreated(
    long Id,
    string DisplayName,
    string Token,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);
