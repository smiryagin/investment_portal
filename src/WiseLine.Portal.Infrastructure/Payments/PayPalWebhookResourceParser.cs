using System.Text.Json;

namespace WiseLine.Portal.Infrastructure.Payments;

internal static class PayPalWebhookResourceParser
{
    public static string GetCompletedSaleSubscriptionId(JsonElement resource)
    {
        if (TryGetString(resource, "billing_agreement_id") is { } billingAgreementId)
        {
            return billingAgreementId;
        }

        if (resource.TryGetProperty("supplementary_data", out var supplementaryData) &&
            supplementaryData.TryGetProperty("related_ids", out var relatedIds) &&
            (TryGetString(relatedIds, "subscription_id") ??
             TryGetString(relatedIds, "billing_agreement_id")) is { } relatedId)
        {
            return relatedId;
        }

        throw new InvalidOperationException(
            "PayPal completed-sale payload is missing the subscription identifier.");
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
}
