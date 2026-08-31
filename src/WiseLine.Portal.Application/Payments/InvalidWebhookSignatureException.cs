namespace WiseLine.Portal.Application.Payments;

public sealed class InvalidWebhookSignatureException(string message) : Exception(message);
