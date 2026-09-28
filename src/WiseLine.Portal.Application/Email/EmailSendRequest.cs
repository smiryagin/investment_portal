namespace WiseLine.Portal.Application.Email;

public sealed record EmailSendRequest(
    string ToAddress,
    string Subject,
    string HtmlBody,
    string TextBody,
    string IdempotencyKey);
