namespace WiseLine.Portal.Application.Payments;

public sealed class PaymentProviderUnavailableException(string message) : Exception(message);
