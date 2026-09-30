using System.Net;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Infrastructure.Payments;

namespace WiseLine.Portal.Api.Tests;

public sealed class PaymentCheckoutServiceTests
{
    [Fact]
    public async Task CreateStripeBillingPortalAsync_CreatesSessionForCustomer()
    {
        var handler = new RecordingHandler(
            """
            {
              "id": "bps_test_123",
              "url": "https://billing.stripe.test/session/123"
            }
            """);
        var service = new PaymentCheckoutService(
            new TestHttpClientFactory(handler),
            Options.Create(new PaymentOptions
            {
                Stripe = new StripeOptions
                {
                    Enabled = true,
                    SecretKey = "sk_test_secret",
                    PriceId = "price_test",
                    WebhookSecret = "whsec_test"
                }
            }));

        var session = await service.CreateStripeBillingPortalAsync(
            "cus_test_123",
            new Uri("https://staging.wiselinetrade.com/account?billing=returned"));

        Assert.Equal("bps_test_123", session.ExternalSessionId);
        Assert.Equal("https://billing.stripe.test/session/123", session.RedirectUrl.ToString());
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://api.stripe.com/v1/billing_portal/sessions",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sk_test_secret", handler.AuthorizationParameter);
        Assert.Contains("customer=cus_test_123", handler.Body, StringComparison.Ordinal);
        Assert.Contains(
            "return_url=https%3A%2F%2Fstaging.wiselinetrade.com%2Faccount%3Fbilling%3Dreturned",
            handler.Body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPayPalSubscriptionAsync_UsesAuthenticatedSubscriptionEndpoint()
    {
        var handler = new RecordingHandler(
            """
            {
              "id": "I-TEST-SUBSCRIPTION",
              "status": "ACTIVE",
              "billing_info": {
                "next_billing_time": "2026-11-14T18:00:00Z"
              }
            }
            """);
        using var client = new HttpClient(handler, disposeHandler: false);

        var result = await PaymentCheckoutService.GetPayPalSubscriptionAsync(
            client,
            new PayPalOptions
            {
                BaseUrl = "https://api-m.sandbox.paypal.com"
            },
            "sandbox-access-token",
            "I-TEST-SUBSCRIPTION",
            CancellationToken.None);

        Assert.Equal("I-TEST-SUBSCRIPTION", result.GetProperty("id").GetString());
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(
            "https://api-m.sandbox.paypal.com/v1/billing/subscriptions/I-TEST-SUBSCRIPTION",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sandbox-access-token", handler.AuthorizationParameter);
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        }
    }
}
