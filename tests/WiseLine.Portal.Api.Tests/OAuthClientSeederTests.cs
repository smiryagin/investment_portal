using OpenIddict.Abstractions;
using WiseLine.Portal.Infrastructure.OAuth;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WiseLine.Portal.Api.Tests;

public sealed class OAuthClientSeederTests
{
    [Fact]
    public void CreatePermissions_AllowsTheConfiguredMcpResource()
    {
        const string resource = "https://staging-investments-mcp.wiselinetrade.com/mcp";

        var permissions = OAuthClientSeeder.CreatePermissions(resource);

        Assert.Contains(Permissions.Prefixes.Resource + resource, permissions);
        Assert.Contains(Permissions.Prefixes.Scope + "investments.read", permissions);
        Assert.Contains(Permissions.Prefixes.Scope + "investments.write", permissions);
    }
}
