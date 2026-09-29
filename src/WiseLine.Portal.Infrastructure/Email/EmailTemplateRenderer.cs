using System.Text.Encodings.Web;

namespace WiseLine.Portal.Infrastructure.Email;

internal static class EmailTemplateRenderer
{
    public static RenderedEmail EmailConfirmation(string displayName, string confirmationUrl)
    {
        var safeName = Encode(displayName);
        var safeUrl = Encode(confirmationUrl);
        const string subject = "Confirm your WiseLine Trade email";
        var content = $"""
            <h1 style="margin:0 0 18px 0;font-family:Arial,Helvetica,sans-serif;font-size:30px;line-height:38px;color:#123b2c;">Confirm your email</h1>
            <p style="margin:0 0 18px 0;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:25px;color:#42594f;">Hello {safeName}, confirm this email address to secure your WiseLine Trade account and receive important account notifications.</p>
            {ActionButton(safeUrl, "Confirm email")}
            <p style="margin:22px 0 0 0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:20px;color:#687b73;">If you did not create this account, you can safely ignore this message.</p>
            """;
        var text = $"Hello {displayName},\n\nConfirm your WiseLine Trade email address:\n{confirmationUrl}\n\nIf you did not create this account, ignore this message.";
        return new RenderedEmail(subject, Layout("Email confirmation", content), text);
    }

    public static RenderedEmail PasswordReset(string displayName, string resetUrl)
    {
        var safeName = Encode(displayName);
        var safeUrl = Encode(resetUrl);
        const string subject = "Reset your WiseLine Trade password";
        var content = $"""
            <h1 style="margin:0 0 18px 0;font-family:Arial,Helvetica,sans-serif;font-size:30px;line-height:38px;color:#123b2c;">Reset your password</h1>
            <p style="margin:0 0 18px 0;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:25px;color:#42594f;">Hello {safeName}, we received a request to reset your WiseLine Trade password.</p>
            {ActionButton(safeUrl, "Choose a new password")}
            <p style="margin:22px 0 0 0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:20px;color:#687b73;">If you did not request this change, ignore this message. Your existing password will continue to work.</p>
            """;
        var text = $"Hello {displayName},\n\nReset your WiseLine Trade password:\n{resetUrl}\n\nIf you did not request this change, ignore this message.";
        return new RenderedEmail(subject, Layout("Password reset", content), text);
    }

    public static RenderedEmail Welcome(string displayName, string accountUrl)
    {
        var safeName = Encode(displayName);
        var safeUrl = Encode(accountUrl);
        const string subject = "Welcome to WiseLine Trade";
        var content = $"""
            <h1 style="margin:0 0 18px 0;font-family:Arial,Helvetica,sans-serif;font-size:30px;line-height:38px;color:#123b2c;">Welcome to WiseLine Trade</h1>
            <p style="margin:0 0 18px 0;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:25px;color:#42594f;">Hello {safeName}, your account is ready. Add a payment method from your Account page to start your 14-day trial and unlock MCP access. You will not be charged today; billing begins at $10 per month after the trial unless you cancel.</p>
            {ActionButton(safeUrl, "Add payment method")}
            <p style="margin:22px 0 0 0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:20px;color:#687b73;">WiseLine provides investment context and tooling, not individualized financial advice.</p>
            """;
        var text = $"Hello {displayName},\n\nWelcome to WiseLine Trade. Add a payment method to start your 14-day trial and unlock MCP access. You will not be charged today; billing begins at $10 per month after the trial unless you cancel.\n\nAdd a payment method:\n{accountUrl}\n\nWiseLine provides investment context and tooling, not individualized financial advice.";
        return new RenderedEmail(subject, Layout("Welcome", content), text);
    }

    private static string Layout(string previewText, string content) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <meta http-equiv="X-UA-Compatible" content="IE=edge">
          <title>{Encode(previewText)}</title>
        </head>
        <body style="margin:0;background-color:#f5f7f3;">
          <table width="100%" cellpadding="0" cellspacing="0" border="0" role="presentation" style="width:100%;background-color:#f5f7f3;">
            <tr>
              <td align="center" bgcolor="#f5f7f3" style="padding-top:32px;padding-right:16px;padding-bottom:32px;padding-left:16px;background-color:#f5f7f3;">
                <table width="100%" cellpadding="0" cellspacing="0" border="0" role="presentation" style="width:100%;max-width:600px;background-color:#ffffff;border:1px solid #dfe7e1;border-radius:14px;">
                  <tr>
                    <td bgcolor="#123b2c" style="padding-top:20px;padding-right:28px;padding-bottom:20px;padding-left:28px;background-color:#123b2c;border-radius:14px 14px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:20px;line-height:26px;font-weight:700;color:#ffffff;">WiseLine <span style="font-family:Arial,Helvetica,sans-serif;font-size:20px;line-height:26px;color:#77d7ab;">Trade</span></td>
                  </tr>
                  <tr>
                    <td style="padding-top:34px;padding-right:28px;padding-bottom:34px;padding-left:28px;">{content}</td>
                  </tr>
                </table>
                <p style="margin:18px 0 0 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#7a8a83;">Automated account notification from WiseLine Trade.</p>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    private static string ActionButton(string url, string label) => $"""
        <table cellpadding="0" cellspacing="0" border="0" role="presentation">
          <tr>
            <td bgcolor="#1f7655" style="background-color:#1f7655;border-radius:8px;padding-top:13px;padding-right:20px;padding-bottom:13px;padding-left:20px;">
              <a href="{url}" style="font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:20px;font-weight:700;color:#ffffff;text-decoration:none;">{Encode(label)}</a>
            </td>
          </tr>
        </table>
        """;

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}

internal sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
