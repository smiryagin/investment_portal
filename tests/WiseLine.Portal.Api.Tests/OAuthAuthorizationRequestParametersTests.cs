using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using WiseLine.Portal.Api.OAuth;

namespace WiseLine.Portal.Api.Tests;

public sealed class OAuthAuthorizationRequestParametersTests
{
    [Fact]
    public void Create_PreservesAuthorizationRequestIncludingRepeatedResources()
    {
        var request = new OpenIddictRequest(new Dictionary<string, StringValues>
        {
            [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
            [OpenIddictConstants.Parameters.ClientId] = "wiseline-codex-cli",
            [OpenIddictConstants.Parameters.RedirectUri] = "http://127.0.0.1:59392/callback",
            [OpenIddictConstants.Parameters.Scope] =
                "openid offline_access investments.read investments.write",
            [OpenIddictConstants.Parameters.State] = "state-value",
            [OpenIddictConstants.Parameters.CodeChallenge] = "code-challenge",
            [OpenIddictConstants.Parameters.CodeChallengeMethod] =
                OpenIddictConstants.CodeChallengeMethods.Sha256,
            [OpenIddictConstants.Parameters.Resource] = new StringValues([
                "https://staging-investments-mcp.wiselinetrade.com/mcp",
                "https://staging-investments-mcp.wiselinetrade.com/mcp"
            ])
        });

        var parameters = OAuthAuthorizationRequestParameters.Create(request);

        Assert.Contains(parameters, parameter =>
            parameter.Name == OpenIddictConstants.Parameters.ClientId &&
            parameter.Value == "wiseline-codex-cli");
        Assert.Contains(parameters, parameter =>
            parameter.Name == OpenIddictConstants.Parameters.CodeChallenge &&
            parameter.Value == "code-challenge");
        Assert.Equal(
            2,
            parameters.Count(parameter =>
                parameter.Name == OpenIddictConstants.Parameters.Resource));
    }
}
