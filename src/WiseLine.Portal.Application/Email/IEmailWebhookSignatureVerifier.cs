namespace WiseLine.Portal.Application.Email;

public interface IEmailWebhookSignatureVerifier
{
    bool IsValid(string payload, string messageId, string timestamp, string signature);
}
