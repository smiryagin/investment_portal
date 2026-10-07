namespace WiseLine.Portal.Api.Models;

public sealed record OAuthAuthorizationViewModel(
    string ClientName,
    IReadOnlyList<OAuthScopeViewModel> Scopes,
    IReadOnlyList<OAuthRequestParameterViewModel> RequestParameters);

public sealed record OAuthScopeViewModel(string Name, string Description);

public sealed record OAuthRequestParameterViewModel(string Name, string Value);
