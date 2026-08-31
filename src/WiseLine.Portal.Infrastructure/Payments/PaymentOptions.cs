namespace WiseLine.Portal.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public string PublicBaseUrl { get; init; } = "https://wiselinetrade.com";

    public StripeOptions Stripe { get; init; } = new();

    public PayPalOptions PayPal { get; init; } = new();
}

public sealed class StripeOptions
{
    public bool Enabled { get; init; }

    public string SecretKey { get; init; } = string.Empty;

    public string PriceId { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;
}

public sealed class PayPalOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = "https://api-m.sandbox.paypal.com";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string PlanId { get; init; } = string.Empty;

    public string WebhookId { get; init; } = string.Empty;
}
