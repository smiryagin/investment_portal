namespace WiseLine.Portal.Application.Email;

public interface IEmailSender
{
    Task<string> SendAsync(EmailSendRequest request, CancellationToken cancellationToken);
}
