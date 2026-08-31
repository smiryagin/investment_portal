using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace WiseLine.Portal.Api.Tests;

public sealed class ApiSmokeTests : IClassFixture<PortalApiFactory>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(PortalApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task LiveHealthCheck_DoesNotRequireDatabaseConnectivity()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class PortalApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PortalDatabase"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=WiseLinePortalTests;Trusted_Connection=True;TrustServerCertificate=True"
            });
        });
    }
}
