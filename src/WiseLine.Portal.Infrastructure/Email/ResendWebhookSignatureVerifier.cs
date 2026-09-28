using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Email;

namespace WiseLine.Portal.Infrastructure.Email;

public sealed class ResendWebhookSignatureVerifier(
    IOptions<EmailOptions> options,
    TimeProvider timeProvider) : IEmailWebhookSignatureVerifier
{
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);
    private readonly EmailOptions _options = options.Value;

    public bool IsValid(string payload, string messageId, string timestamp, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret) ||
            string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(messageId) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            string.IsNullOrWhiteSpace(signature) ||
            !long.TryParse(timestamp, out var timestampSeconds))
        {
            return false;
        }

        DateTimeOffset signedAt;
        try
        {
            signedAt = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if ((timeProvider.GetUtcNow() - signedAt).Duration() > AllowedClockSkew)
        {
            return false;
        }

        byte[] secret;
        try
        {
            secret = DecodeSecret(_options.WebhookSecret);
        }
        catch (FormatException)
        {
            return false;
        }

        var signedContent = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.{payload}");
        var expectedSignature = HMACSHA256.HashData(secret, signedContent);

        foreach (var value in signature.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = value.IndexOf(',');
            if (separator <= 0 || !value[..separator].Equals("v1", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var suppliedSignature = Convert.FromBase64String(value[(separator + 1)..]);
                if (suppliedSignature.Length == expectedSignature.Length &&
                    CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                {
                    return true;
                }
            }
            catch (FormatException)
            {
                // Ignore malformed signature candidates and continue checking any remaining v1 values.
            }
        }

        return false;
    }

    private static byte[] DecodeSecret(string secret)
    {
        const string prefix = "whsec_";
        var encoded = secret.StartsWith(prefix, StringComparison.Ordinal) ? secret[prefix.Length..] : secret;
        encoded = encoded.Replace('-', '+').Replace('_', '/');
        encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
        return Convert.FromBase64String(encoded);
    }
}
