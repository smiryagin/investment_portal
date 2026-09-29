using WiseLine.Portal.Infrastructure.Email;

namespace WiseLine.Portal.Api.Tests;

public sealed class EmailTemplateRendererTests
{
    [Fact]
    public void Welcome_ExplainsHowAndWhenTheTrialStarts()
    {
        const string accountUrl = "https://staging.wiselinetrade.com/account";

        var rendered = EmailTemplateRenderer.Welcome("Andrey", accountUrl);

        Assert.Contains("payment method", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("14-day trial", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$10 per month", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not be charged today", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(accountUrl, rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("payment method", rendered.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(accountUrl, rendered.TextBody, StringComparison.Ordinal);
    }
}
