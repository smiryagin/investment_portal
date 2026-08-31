using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Payments;
using WiseLine.Portal.Domain.Payments;
using WiseLine.Portal.Domain.Subscriptions;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Payments;

public sealed class PaymentWebhookProcessor(
    PortalDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IOptions<PaymentOptions> options,
    TimeProvider timeProvider) : IPaymentWebhookProcessor
{
    private readonly PaymentOptions _options = options.Value;

    public async Task ProcessStripeAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        VerifyStripeSignature(payload, signatureHeader);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventId = GetRequiredString(root, "id");
        var eventType = GetRequiredString(root, "type");
        var webhookEvent = await BeginEventAsync("Stripe", eventId, eventType, payload, cancellationToken);
        if (webhookEvent is null)
        {
            return;
        }

        try
        {
            if (eventType.StartsWith("customer.subscription.", StringComparison.Ordinal))
            {
                var resource = root.GetProperty("data").GetProperty("object");
                await ApplyStripeSubscriptionAsync(resource, cancellationToken);
            }

            webhookEvent.MarkProcessed(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            webhookEvent.MarkFailed(exception.Message[..Math.Min(exception.Message.Length, 1900)]);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task ProcessPayPalAsync(
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        await VerifyPayPalSignatureAsync(root, headers, cancellationToken);

        var eventId = GetRequiredString(root, "id");
        var eventType = GetRequiredString(root, "event_type");
        var webhookEvent = await BeginEventAsync("PayPal", eventId, eventType, payload, cancellationToken);
        if (webhookEvent is null)
        {
            return;
        }

        try
        {
            if (eventType.StartsWith("BILLING.SUBSCRIPTION.", StringComparison.Ordinal))
            {
                await ApplyPayPalSubscriptionAsync(eventType, root.GetProperty("resource"), cancellationToken);
            }

            webhookEvent.MarkProcessed(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            webhookEvent.MarkFailed(exception.Message[..Math.Min(exception.Message.Length, 1900)]);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private void VerifyStripeSignature(string payload, string signatureHeader)
    {
        var secret = _options.Stripe.WebhookSecret;
        if (!_options.Stripe.Enabled || string.IsNullOrWhiteSpace(secret))
        {
            throw new PaymentProviderUnavailableException("Stripe webhooks are not configured.");
        }

        StripeWebhookSignatureVerifier.Verify(
            payload,
            signatureHeader,
            secret,
            timeProvider.GetUtcNow());
    }

    private async Task VerifyPayPalSignatureAsync(
        JsonElement webhookEvent,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var payPal = _options.PayPal;
        if (!payPal.Enabled || string.IsNullOrWhiteSpace(payPal.WebhookId))
        {
            throw new PaymentProviderUnavailableException("PayPal webhooks are not configured.");
        }

        string RequiredHeader(string name) => headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidWebhookSignatureException($"PayPal header {name} is missing.");

        var client = httpClientFactory.CreateClient("PayPal");
        var accessToken = await PaymentCheckoutService.GetPayPalAccessTokenAsync(client, payPal, cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(payPal.BaseUrl.TrimEnd('/') + "/"), "v1/notifications/verify-webhook-signature"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new Dictionary<string, object?>
        {
            ["auth_algo"] = RequiredHeader("PAYPAL-AUTH-ALGO"),
            ["cert_url"] = RequiredHeader("PAYPAL-CERT-URL"),
            ["transmission_id"] = RequiredHeader("PAYPAL-TRANSMISSION-ID"),
            ["transmission_sig"] = RequiredHeader("PAYPAL-TRANSMISSION-SIG"),
            ["transmission_time"] = RequiredHeader("PAYPAL-TRANSMISSION-TIME"),
            ["webhook_id"] = payPal.WebhookId,
            ["webhook_event"] = webhookEvent.Clone()
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidWebhookSignatureException("PayPal could not verify the webhook signature.");
        }

        using var verification = JsonDocument.Parse(responseBody);
        if (!string.Equals(
                verification.RootElement.GetProperty("verification_status").GetString(),
                "SUCCESS",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidWebhookSignatureException("PayPal webhook signature is invalid.");
        }
    }

    private async Task ApplyStripeSubscriptionAsync(JsonElement resource, CancellationToken cancellationToken)
    {
        var providerSubscriptionId = GetRequiredString(resource, "id");
        var subscription = await FindSubscriptionAsync(
            PaymentProvider.Stripe,
            providerSubscriptionId,
            TryGetPortalUserId(resource),
            cancellationToken);

        if (subscription is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var status = GetRequiredString(resource, "status");
        var customerId = GetOptionalString(resource, "customer") ?? subscription.ProviderCustomerId ?? "unknown";
        var periodEnd = GetStripePeriodEnd(resource) ?? now.AddMonths(1);
        var trialEnd = GetUnixDate(resource, "trial_end");

        switch (status)
        {
            case "trialing" when trialEnd is { } end && end > now:
                subscription.BeginTrial(PaymentProvider.Stripe, customerId, providerSubscriptionId, end, now);
                break;
            case "active":
                subscription.Activate(PaymentProvider.Stripe, customerId, providerSubscriptionId, periodEnd, now);
                break;
            case "past_due":
            case "unpaid":
                subscription.MarkPastDue(now);
                break;
            case "canceled":
                subscription.Cancel(now);
                break;
            case "incomplete_expired":
                subscription.Expire(now);
                break;
        }

        if (resource.TryGetProperty("cancel_at_period_end", out var cancelAtPeriodEnd) && cancelAtPeriodEnd.ValueKind == JsonValueKind.True)
        {
            subscription.ScheduleCancellation(now);
        }
    }

    private async Task ApplyPayPalSubscriptionAsync(
        string eventType,
        JsonElement resource,
        CancellationToken cancellationToken)
    {
        var providerSubscriptionId = GetRequiredString(resource, "id");
        var portalUserId = Guid.TryParse(GetOptionalString(resource, "custom_id"), out var parsed) ? parsed : (Guid?)null;
        var subscription = await FindSubscriptionAsync(
            PaymentProvider.PayPal,
            providerSubscriptionId,
            portalUserId,
            cancellationToken);

        if (subscription is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var customerId = resource.TryGetProperty("subscriber", out var subscriber)
            ? GetOptionalString(subscriber, "payer_id") ?? subscription.ProviderCustomerId ?? "unknown"
            : subscription.ProviderCustomerId ?? "unknown";
        var nextBilling = GetNestedDate(resource, "billing_info", "next_billing_time") ?? now.AddMonths(1);

        switch (eventType)
        {
            case "BILLING.SUBSCRIPTION.ACTIVATED":
                if (subscription.Status == SubscriptionStatus.Pending)
                {
                    var trialEnd = nextBilling > now ? nextBilling : now.AddDays(PlanCatalog.StandardTrialDays);
                    subscription.BeginTrial(PaymentProvider.PayPal, customerId, providerSubscriptionId, trialEnd, now);
                }
                else
                {
                    subscription.Activate(PaymentProvider.PayPal, customerId, providerSubscriptionId, nextBilling, now);
                }
                break;
            case "BILLING.SUBSCRIPTION.SUSPENDED":
            case "BILLING.SUBSCRIPTION.PAYMENT.FAILED":
                subscription.MarkPastDue(now);
                break;
            case "BILLING.SUBSCRIPTION.CANCELLED":
                subscription.Cancel(now);
                break;
            case "BILLING.SUBSCRIPTION.EXPIRED":
                subscription.Expire(now);
                break;
        }
    }

    private async Task<Subscription?> FindSubscriptionAsync(
        PaymentProvider provider,
        string providerSubscriptionId,
        Guid? portalUserId,
        CancellationToken cancellationToken)
    {
        if (portalUserId is { } userId)
        {
            return await dbContext.Subscriptions.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        }

        return await dbContext.Subscriptions.SingleOrDefaultAsync(
            x => x.Provider == provider && x.ProviderSubscriptionId == providerSubscriptionId,
            cancellationToken);
    }

    private async Task<PaymentWebhookEvent?> BeginEventAsync(
        string provider,
        string eventId,
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        if (await dbContext.PaymentWebhookEvents.AnyAsync(
                x => x.Provider == provider && x.ProviderEventId == eventId,
                cancellationToken))
        {
            return null;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var webhookEvent = new PaymentWebhookEvent(provider, eventId, eventType, hash, timeProvider.GetUtcNow());
        dbContext.PaymentWebhookEvents.Add(webhookEvent);
        return webhookEvent;
    }

    private static Guid? TryGetPortalUserId(JsonElement resource)
    {
        if (!resource.TryGetProperty("metadata", out var metadata))
        {
            return null;
        }

        return Guid.TryParse(GetOptionalString(metadata, "portal_user_id"), out var userId) ? userId : null;
    }

    private static DateTimeOffset? GetStripePeriodEnd(JsonElement resource)
    {
        var direct = GetUnixDate(resource, "current_period_end");
        if (direct is not null)
        {
            return direct;
        }

        if (resource.TryGetProperty("items", out var items) &&
            items.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array &&
            data.GetArrayLength() > 0)
        {
            return GetUnixDate(data[0], "current_period_end");
        }

        return null;
    }

    private static DateTimeOffset? GetUnixDate(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt64(out var timestamp) ? DateTimeOffset.FromUnixTimeSeconds(timestamp) : null;
    }

    private static DateTimeOffset? GetNestedDate(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var nested))
        {
            return null;
        }

        var value = GetOptionalString(nested, propertyName);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
    }

    private static string GetRequiredString(JsonElement element, string propertyName) =>
        GetOptionalString(element, propertyName)
        ?? throw new InvalidOperationException($"Payment payload is missing {propertyName}.");

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
