namespace WiseLine.Portal.Application.Email;

public interface ITransactionalEmailOutbox
{
    void QueueEmailConfirmation(
        Guid userId,
        string email,
        string displayName,
        string confirmationUrl,
        DateTimeOffset now);

    void QueuePasswordReset(
        Guid userId,
        string email,
        string displayName,
        string resetUrl,
        DateTimeOffset now);

    void QueueWelcome(
        Guid userId,
        string email,
        string displayName,
        DateTimeOffset now);
}
