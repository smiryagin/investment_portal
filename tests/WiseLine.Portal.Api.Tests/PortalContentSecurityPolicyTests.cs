using Microsoft.Extensions.Configuration;
using WiseLine.Portal.Api.Security;

namespace WiseLine.Portal.Api.Tests;

public sealed class PortalContentSecurityPolicyTests
{
    [Fact]
    public void Create_AllowsRegisteredWebAndNativeOAuthCallbacks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth:Clients:0:RedirectUris:0"] =
                    "https://chatgpt.com/connector_platform_oauth_redirect",
                ["OAuth:Clients:1:RedirectUris:0"] = "http://127.0.0.1/callback"
            })
            .Build();

        var policy = PortalContentSecurityPolicy.Create(configuration);

        Assert.Contains("https://chatgpt.com", policy, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:*", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_DoesNotAllowUnregisteredInsecureRemoteCallbacks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth:Clients:0:RedirectUris:0"] = "http://example.test/callback"
            })
            .Build();

        var policy = PortalContentSecurityPolicy.Create(configuration);

        Assert.DoesNotContain("http://example.test", policy, StringComparison.Ordinal);
    }
}
