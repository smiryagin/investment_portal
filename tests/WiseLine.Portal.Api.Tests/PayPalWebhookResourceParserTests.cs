using System.Text.Json;
using WiseLine.Portal.Infrastructure.Payments;

namespace WiseLine.Portal.Api.Tests;

public sealed class PayPalWebhookResourceParserTests
{
    [Fact]
    public void GetCompletedSaleSubscriptionId_ReadsBillingAgreementId()
    {
        using var document = JsonDocument.Parse("""
            {
              "id": "SALE-TEST",
              "billing_agreement_id": "I-PRIMARY"
            }
            """);

        var result = PayPalWebhookResourceParser.GetCompletedSaleSubscriptionId(
            document.RootElement);

        Assert.Equal("I-PRIMARY", result);
    }

    [Fact]
    public void GetCompletedSaleSubscriptionId_ReadsRelatedSubscriptionId()
    {
        using var document = JsonDocument.Parse("""
            {
              "supplementary_data": {
                "related_ids": {
                  "subscription_id": "I-RELATED"
                }
              }
            }
            """);

        var result = PayPalWebhookResourceParser.GetCompletedSaleSubscriptionId(
            document.RootElement);

        Assert.Equal("I-RELATED", result);
    }

    [Fact]
    public void GetCompletedSaleSubscriptionId_RejectsMissingIdentifier()
    {
        using var document = JsonDocument.Parse("{\"id\":\"SALE-TEST\"}");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PayPalWebhookResourceParser.GetCompletedSaleSubscriptionId(document.RootElement));

        Assert.Contains("subscription identifier", exception.Message, StringComparison.Ordinal);
    }
}
