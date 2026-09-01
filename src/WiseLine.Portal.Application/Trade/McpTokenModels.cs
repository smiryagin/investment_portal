namespace WiseLine.Portal.Application.Trade;

public sealed record McpTokenSummary(
    Guid Id,
    string DisplayName,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked);

public sealed record McpTokenCreated(
    Guid Id,
    string DisplayName,
    string Token,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);
