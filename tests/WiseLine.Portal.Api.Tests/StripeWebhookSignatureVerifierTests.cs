using System.Security.Cryptography;
using System.Text;
using WiseLine.Portal.Application.Payments;
using WiseLine.Portal.Infrastructure.Payments;

namespace WiseLine.Portal.Api.Tests;

public sealed class StripeWebhookSignatureVerifierTests
{
    private const string Secret = "whsec_test_secret";
    private const string Payload = "{\"id\":\"evt_test\"}";
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Verify_AcceptsMatchingSignatureWithinTolerance()
    {
        var header = CreateHeader(Payload, Now);

        StripeWebhookSignatureVerifier.Verify(Payload, header, Secret, Now);
    }

    [Fact]
    public void Verify_RejectsModifiedPayload()
    {
        var header = CreateHeader(Payload, Now);

        Assert.Throws<InvalidWebhookSignatureException>(() =>
            StripeWebhookSignatureVerifier.Verify(Payload + " ", header, Secret, Now));
    }

    [Fact]
    public void Verify_RejectsStaleTimestamp()
    {
        var header = CreateHeader(Payload, Now.AddMinutes(-6));

        Assert.Throws<InvalidWebhookSignatureException>(() =>
            StripeWebhookSignatureVerifier.Verify(Payload, header, Secret, Now));
    }

    private static string CreateHeader(string payload, DateTimeOffset timestamp)
    {
        var unixTime = timestamp.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var signature = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"{unixTime}.{payload}"))).ToLowerInvariant();
        return $"t={unixTime},v1={signature}";
    }
}
