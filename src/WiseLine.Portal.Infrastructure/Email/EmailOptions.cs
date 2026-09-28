namespace WiseLine.Portal.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "WiseLine Trade <no-reply@email.wiselinetrade.com>";

    public string ReplyToAddress { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = "https://wiselinetrade.com";

    public string WebhookSecret { get; set; } = string.Empty;

    public int OutboxPollSeconds { get; set; } = 5;
}
