using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Payments;
using WiseLine.Portal.Domain.Subscriptions;

namespace WiseLine.Portal.Infrastructure.Payments;

public sealed class PaymentCheckoutService(
    IHttpClientFactory httpClientFactory,
    IOptions<PaymentOptions> options) : IPaymentCheckoutService
{
    private readonly PaymentOptions _options = options.Value;

    public Task<CheckoutSession> CreateCheckoutAsync(
        Guid userId,
        string email,
        PaymentProvider provider,
        Uri returnUrl,
        Uri cancelUrl,
        CancellationToken cancellationToken = default) => provider switch
    {
        PaymentProvider.Stripe => CreateStripeCheckoutAsync(userId, email, returnUrl, cancelUrl, cancellationToken),
        PaymentProvider.PayPal => CreatePayPalCheckoutAsync(userId, email, returnUrl, cancelUrl, cancellationToken),
        _ => throw new PaymentProviderUnavailableException("The selected payment provider is not supported.")
    };

    private async Task<CheckoutSession> CreateStripeCheckoutAsync(
        Guid userId,
        string email,
        Uri returnUrl,
        Uri cancelUrl,
        CancellationToken cancellationToken)
    {
        var stripe = _options.Stripe;
        if (!stripe.Enabled || string.IsNullOrWhiteSpace(stripe.SecretKey) || string.IsNullOrWhiteSpace(stripe.PriceId))
        {
            throw new PaymentProviderUnavailableException("Stripe checkout is not configured for this environment.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stripe.SecretKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"wiseline-{userId:N}-monthly");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["customer_email"] = email,
            ["client_reference_id"] = userId.ToString(),
            ["line_items[0][price]"] = stripe.PriceId,
            ["line_items[0][quantity]"] = "1",
            ["payment_method_collection"] = "always",
            ["subscription_data[trial_period_days]"] = PlanCatalog.StandardTrialDays.ToString(),
            ["subscription_data[trial_settings][end_behavior][missing_payment_method]"] = "cancel",
            ["subscription_data[metadata][portal_user_id]"] = userId.ToString(),
            ["metadata[portal_user_id]"] = userId.ToString(),
            ["success_url"] = returnUrl.ToString(),
            ["cancel_url"] = cancelUrl.ToString()
        });

        var client = httpClientFactory.CreateClient("Stripe");
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderUnavailableException(
                $"Stripe checkout returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        var url = root.GetProperty("url").GetString();
        if (string.IsNullOrWhiteSpace(id) || !Uri.TryCreate(url, UriKind.Absolute, out var redirectUrl))
        {
            throw new PaymentProviderUnavailableException("Stripe did not return a valid checkout session.");
        }

        return new CheckoutSession(redirectUrl, id);
    }

    private async Task<CheckoutSession> CreatePayPalCheckoutAsync(
        Guid userId,
        string email,
        Uri returnUrl,
        Uri cancelUrl,
        CancellationToken cancellationToken)
    {
        var payPal = _options.PayPal;
        if (!payPal.Enabled ||
            string.IsNullOrWhiteSpace(payPal.ClientId) ||
            string.IsNullOrWhiteSpace(payPal.ClientSecret) ||
            string.IsNullOrWhiteSpace(payPal.PlanId))
        {
            throw new PaymentProviderUnavailableException("PayPal checkout is not configured for this environment.");
        }

        var client = httpClientFactory.CreateClient("PayPal");
        var accessToken = await GetPayPalAccessTokenAsync(client, payPal, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(payPal.BaseUrl.TrimEnd('/') + "/"), "v1/billing/subscriptions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("PayPal-Request-Id", userId.ToString("N"));
        request.Content = JsonContent.Create(new
        {
            plan_id = payPal.PlanId,
            custom_id = userId.ToString(),
            subscriber = new { email_address = email },
            application_context = new
            {
                brand_name = "WiseLine Trade",
                user_action = "SUBSCRIBE_NOW",
                return_url = returnUrl.ToString(),
                cancel_url = cancelUrl.ToString()
            }
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderUnavailableException(
                $"PayPal checkout returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        string? approvalUrl = null;
        foreach (var link in root.GetProperty("links").EnumerateArray())
        {
            if (string.Equals(link.GetProperty("rel").GetString(), "approve", StringComparison.OrdinalIgnoreCase))
            {
                approvalUrl = link.GetProperty("href").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(id) || !Uri.TryCreate(approvalUrl, UriKind.Absolute, out var redirectUrl))
        {
            throw new PaymentProviderUnavailableException("PayPal did not return a valid approval link.");
        }

        return new CheckoutSession(redirectUrl, id);
    }

    internal static async Task<string> GetPayPalAccessTokenAsync(
        HttpClient client,
        PayPalOptions payPal,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(payPal.BaseUrl.TrimEnd('/') + "/"), "v1/oauth2/token"));
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payPal.ClientId}:{payPal.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderUnavailableException("PayPal authentication failed.");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new PaymentProviderUnavailableException("PayPal did not return an access token.");
    }
}
