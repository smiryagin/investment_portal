using System.Security.Cryptography;
using System.Text;
using WiseLine.Portal.Application.Payments;

namespace WiseLine.Portal.Infrastructure.Payments;

internal static class StripeWebhookSignatureVerifier
{
    public static void Verify(
        string payload,
        string signatureHeader,
        string secret,
        DateTimeOffset now,
        TimeSpan? tolerance = null)
    {
        var values = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToLookup(part => part[0], part => part[1]);

        if (!long.TryParse(values["t"].FirstOrDefault(), out var timestamp))
        {
            throw new InvalidWebhookSignatureException("Stripe webhook timestamp is missing.");
        }

        var sentAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if ((now - sentAt).Duration() > (tolerance ?? TimeSpan.FromMinutes(5)))
        {
            throw new InvalidWebhookSignatureException("Stripe webhook timestamp is outside the allowed tolerance.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var valid = values["v1"].Any(signature =>
        {
            try
            {
                var supplied = Convert.FromHexString(signature);
                return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        });

        if (!valid)
        {
            throw new InvalidWebhookSignatureException("Stripe webhook signature is invalid.");
        }
    }
}
