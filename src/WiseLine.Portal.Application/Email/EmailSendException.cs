namespace WiseLine.Portal.Application.Email;

public sealed class EmailSendException(string message, bool retryable, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool Retryable { get; } = retryable;
}
