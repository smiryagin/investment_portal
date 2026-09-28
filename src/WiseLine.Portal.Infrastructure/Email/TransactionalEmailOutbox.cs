using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Email;
using WiseLine.Portal.Domain.Email;
using WiseLine.Portal.Infrastructure.Persistence;

namespace WiseLine.Portal.Infrastructure.Email;

public sealed class TransactionalEmailOutbox(
    PortalDbContext dbContext,
    IOptions<EmailOptions> options) : ITransactionalEmailOutbox
{
    private readonly EmailOptions _options = options.Value;

    public void QueueEmailConfirmation(
        Guid userId,
        string email,
        string displayName,
        string confirmationUrl,
        DateTimeOffset now)
    {
        var rendered = EmailTemplateRenderer.EmailConfirmation(displayName, confirmationUrl);
        Queue(userId, email, "email-confirmation", rendered, now);
    }

    public void QueuePasswordReset(
        Guid userId,
        string email,
        string displayName,
        string resetUrl,
        DateTimeOffset now)
    {
        var rendered = EmailTemplateRenderer.PasswordReset(displayName, resetUrl);
        Queue(userId, email, "password-reset", rendered, now);
    }

    public void QueueWelcome(
        Guid userId,
        string email,
        string displayName,
        DateTimeOffset now)
    {
        var accountUrl = new Uri(new Uri(EnsureTrailingSlash(_options.PublicBaseUrl)), "account").ToString();
        var rendered = EmailTemplateRenderer.Welcome(displayName, accountUrl);
        Queue(userId, email, "welcome", rendered, now);
    }

    private void Queue(
        Guid userId,
        string email,
        string templateKey,
        RenderedEmail rendered,
        DateTimeOffset now)
    {
        if (!_options.Enabled)
        {
            return;
        }

        dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage(
            userId,
            email,
            templateKey,
            rendered.Subject,
            rendered.HtmlBody,
            rendered.TextBody,
            now));
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : $"{value}/";
}
