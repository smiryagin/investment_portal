namespace WiseLine.Portal.Api.Contracts;

public sealed record OAuthConnectionResponse(
    string Id,
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? AuthorizedAt);
