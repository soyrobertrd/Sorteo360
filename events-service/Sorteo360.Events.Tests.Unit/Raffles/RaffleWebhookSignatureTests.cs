using System.Security.Cryptography;
using System.Text;
using Sorteo360.Events.Web.RaffleEndpoints;
using Shouldly;

namespace Sorteo360.Events.Tests.Unit.Raffles;

public sealed class RaffleWebhookSignatureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KeyMatches_ReturnsTrueOnlyWhenKeysMatchInConstantTime()
    {
        RaffleWebhookSignature.KeyMatches("super-secret-key", "super-secret-key").ShouldBeTrue();
        RaffleWebhookSignature.KeyMatches("super-secret-key", "wrong-key").ShouldBeFalse();
        RaffleWebhookSignature.KeyMatches(null, "super-secret-key").ShouldBeFalse();
    }

    [Fact]
    public void SignatureMatches_ReturnsTrue_WhenHmacAndTimestampAreWithinTolerance()
    {
        const string secret = "whsec_test_123";
        var body = Encoding.UTF8.GetBytes("""{"lookupCode":"QR-1"}""");
        var header = Sign(secret, Now.AddMinutes(-2), body);

        RaffleWebhookSignature.SignatureMatches(secret, header, body, Now).ShouldBeTrue();
    }

    [Fact]
    public void SignatureMatches_ReturnsFalse_WhenTimestampExceedsFiveMinutes()
    {
        const string secret = "whsec_test_123";
        var body = Encoding.UTF8.GetBytes("""{"lookupCode":"QR-1"}""");
        var header = Sign(secret, Now - RaffleWebhookSignature.Tolerance - TimeSpan.FromSeconds(1), body);

        RaffleWebhookSignature.SignatureMatches(secret, header, body, Now).ShouldBeFalse();
    }

    [Fact]
    public void SignatureMatches_ReturnsFalse_WhenBodyIsTampered()
    {
        const string secret = "whsec_test_123";
        var signedBody = Encoding.UTF8.GetBytes("""{"lookupCode":"QR-1"}""");
        var tamperedBody = Encoding.UTF8.GetBytes("""{"lookupCode":"QR-2"}""");
        var header = Sign(secret, Now, signedBody);

        RaffleWebhookSignature.SignatureMatches(secret, header, tamperedBody, Now).ShouldBeFalse();
    }

    private static string Sign(string secret, DateTimeOffset timestamp, byte[] body)
    {
        var epoch = timestamp.ToUnixTimeSeconds().ToString();
        var prefix = Encoding.ASCII.GetBytes(epoch + ".");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload.AsSpan(prefix.Length));

        var hex = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload));
        return $"t={epoch},v1={hex}";
    }
}

