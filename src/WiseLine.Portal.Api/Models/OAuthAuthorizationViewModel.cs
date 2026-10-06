namespace WiseLine.Portal.Api.Models;

public sealed record OAuthAuthorizationViewModel(
    string ClientName,
    IReadOnlyList<OAuthScopeViewModel> Scopes);

public sealed record OAuthScopeViewModel(string Name, string Description);
