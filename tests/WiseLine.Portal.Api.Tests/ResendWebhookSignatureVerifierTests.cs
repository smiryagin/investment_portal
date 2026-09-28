using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Infrastructure.Email;

namespace WiseLine.Portal.Api.Tests;

public sealed class ResendWebhookSignatureVerifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-28T20:00:00Z");
    private static readonly byte[] SecretBytes = Encoding.UTF8.GetBytes("test-signing-secret");
    private static readonly string Secret = $"whsec_{Convert.ToBase64String(SecretBytes)}";

    [Fact]
    public void IsValid_AcceptsMatchingCurrentSignature()
    {
        const string payload = "{\"type\":\"email.delivered\"}";
        const string messageId = "msg_test";
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = Sign(messageId, timestamp, payload);
        var verifier = CreateVerifier();

        var valid = verifier.IsValid(payload, messageId, timestamp, signature);

        Assert.True(valid);
    }

    [Fact]
    public void IsValid_RejectsModifiedPayload()
    {
        const string payload = "{\"type\":\"email.delivered\"}";
        const string messageId = "msg_test";
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = Sign(messageId, timestamp, payload);
        var verifier = CreateVerifier();

        var valid = verifier.IsValid("{\"type\":\"email.bounced\"}", messageId, timestamp, signature);

        Assert.False(valid);
    }

    [Fact]
    public void IsValid_RejectsExpiredSignature()
    {
        const string payload = "{\"type\":\"email.delivered\"}";
        const string messageId = "msg_test";
        var timestamp = Now.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var signature = Sign(messageId, timestamp, payload);
        var verifier = CreateVerifier();

        var valid = verifier.IsValid(payload, messageId, timestamp, signature);

        Assert.False(valid);
    }

    private static ResendWebhookSignatureVerifier CreateVerifier() =>
        new(
            Options.Create(new EmailOptions { WebhookSecret = Secret }),
            new FixedTimeProvider(Now));

    private static string Sign(string messageId, string timestamp, string payload)
    {
        var content = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.{payload}");
        var signature = HMACSHA256.HashData(SecretBytes, content);
        return $"v1,{Convert.ToBase64String(signature)}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
