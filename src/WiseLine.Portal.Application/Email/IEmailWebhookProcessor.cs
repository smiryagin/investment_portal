namespace WiseLine.Portal.Application.Email;

public interface IEmailWebhookProcessor
{
    Task ProcessAsync(
        string providerEventId,
        string payload,
        CancellationToken cancellationToken);
}
