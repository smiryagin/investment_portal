namespace WiseLine.Portal.Application.Payments;

public interface IPaymentWebhookProcessor
{
    Task ProcessStripeAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default);

    Task ProcessPayPalAsync(
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
