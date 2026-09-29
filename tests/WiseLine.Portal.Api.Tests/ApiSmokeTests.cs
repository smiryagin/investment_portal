using System.Net;
using System.Text;
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
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
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

    [Fact]
    public async Task AngularRoute_IsServedAsHtml()
    {
        var response = await _client.GetAsync("/portfolios/sample");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
    }

    [Fact]
    public async Task UnknownApiRoute_DoesNotReturnAngularApplication()
    {
        var response = await _client.GetAsync("/api/not-a-real-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResendWebhook_RejectsUnsignedRequests()
    {
        using var content = new StringContent("{\"type\":\"email.delivered\"}");

        var response = await _client.PostAsync("/api/webhooks/resend", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CsrfToken_AllowsProtectedPostToReachModelValidation()
    {
        var tokenResponse = await _client.GetAsync("/api/security/csrf");
        var requestToken = tokenResponse.Headers
            .GetValues("Set-Cookie")
            .Select(ParseXsrfRequestToken)
            .FirstOrDefault(value => value is not null);

        Assert.Equal(HttpStatusCode.NoContent, tokenResponse.StatusCode);
        Assert.NotNull(requestToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"errors\"", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Email", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ParseXsrfRequestToken(string setCookieHeader)
    {
        const string prefix = "XSRF-TOKEN=";
        if (!setCookieHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var separator = setCookieHeader.IndexOf(';');
        var encodedValue = separator < 0
            ? setCookieHeader[prefix.Length..]
            : setCookieHeader[prefix.Length..separator];
        return Uri.UnescapeDataString(encodedValue);
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
