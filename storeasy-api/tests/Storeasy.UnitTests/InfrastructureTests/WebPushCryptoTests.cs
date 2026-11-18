using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Infrastructure.Notifications.WebPush;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Web push message encryption (RFC 8291) and VAPID (RFC 8292).")]
public class WebPushCryptoTests
{
    private static (ECDiffieHellman Key, byte[] PublicKey, byte[] Auth) NewBrowser()
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        return (key, WebPushCrypto.ExportPublicKey(key), RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>
    /// The browser's side of RFC 8291, written from the RFC independently of the code under test.
    /// </summary>
    private static byte[] Decrypt(byte[] body, ECDiffieHellman browserKey, byte[] browserPublic, byte[] auth)
    {
        var salt = body[..16];
        var keyIdLength = body[20];
        var serverPublic = body[21..(21 + keyIdLength)];
        var cipher = body[(21 + keyIdLength)..];

        using var serverKey = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..] } });

        var shared = browserKey.DeriveRawSecretAgreement(serverKey.PublicKey);
        var prkKey = HMACSHA256.HashData(auth, shared);
        var ikm = HMACSHA256.HashData(prkKey, (byte[])[.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. browserPublic, .. serverPublic, 0x01])[..32];
        var prk = HMACSHA256.HashData(salt, ikm);
        var cek = HMACSHA256.HashData(prk, (byte[])[.. Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"), 0x01])[..16];
        var nonce = HMACSHA256.HashData(prk, (byte[])[.. Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"), 0x01])[..12];
        var plain = new byte[cipher.Length - 16];

        using var aes = new AesGcm(cek, 16);

        aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);

        return plain;
    }

    [Fact]
    public void Encrypt_ShouldProduceOneAes128GcmRecordTheBrowserCanOpen()
    {
        // Arrange
        var (key, publicKey, auth) = NewBrowser();
        var message = Encoding.UTF8.GetBytes("""{"title":"Red","body":"Defter 1.4 rejected"}""");

        // Act
        var body = WebPushCrypto.Encrypt(message, publicKey, auth);
        var plain = Decrypt(body, key, publicKey, auth);

        // Assert
        BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)).Should().Be(WebPushCrypto.RecordSize);
        body[20].Should().Be(65);
        body[21].Should().Be(0x04);
        plain[^1].Should().Be(0x02, "the last record ends with the 0x02 delimiter");
        plain[..^1].Should().Equal(message);
        key.Dispose();
    }

    [Fact]
    public void Encrypt_ShouldUseANewKeyAndSaltEveryTime()
    {
        var (key, publicKey, auth) = NewBrowser();

        var first = WebPushCrypto.Encrypt("same"u8, publicKey, auth);
        var second = WebPushCrypto.Encrypt("same"u8, publicKey, auth);

        first[..16].Should().NotEqual(second[..16]);
        first[21..86].Should().NotEqual(second[21..86]);
        key.Dispose();
    }

    [Fact]
    public void Encrypt_ShouldRefuseBadKeysAndOversizedPayloads()
    {
        var (key, publicKey, auth) = NewBrowser();

        FluentActions.Invoking(() => WebPushCrypto.Encrypt(new byte[WebPushCrypto.MaxPlaintextLength + 1], publicKey, auth)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => WebPushCrypto.Encrypt("x"u8, publicKey[..64], auth)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => WebPushCrypto.Encrypt("x"u8, publicKey, auth[..8])).Should().Throw<ArgumentException>();
        key.Dispose();
    }

    [Fact]
    public void VapidAuthorization_ShouldCarryAnEs256JwtForThePushServiceOrigin()
    {
        // Arrange
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = vapid.ExportParameters(true);
        var publicKey = Base64Url.EncodeToString([0x04, .. parameters.Q.X, .. parameters.Q.Y]);
        var privateKey = Base64Url.EncodeToString(parameters.D);
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        // Act
        var header = WebPushCrypto.CreateVapidAuthorization(new Uri("https://fcm.googleapis.com/fcm/send/abc"), "mailto:ops@example.com", publicKey, privateKey, now);

        // Assert
        header.Should().StartWith("vapid t=").And.EndWith($", k={publicKey}");

        var jwt = header["vapid t=".Length..header.IndexOf(',')];
        var parts = jwt.Split('.');
        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));

        claims.RootElement.GetProperty("aud").GetString().Should().Be("https://fcm.googleapis.com");
        claims.RootElement.GetProperty("sub").GetString().Should().Be("mailto:ops@example.com");
        claims.RootElement.GetProperty("exp").GetInt64().Should().Be(now.AddHours(12).ToUnixTimeSeconds());
        vapid.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/x", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/x", false)]
    [InlineData("https://user:pass@push.example.com/x", false)]
    [InlineData("https://localhost/x", false)]
    [InlineData("not a url", false)]
    public void IsEndpoint_ShouldAcceptOnlyHttpsPushAddresses(string endpoint, bool expected)
    {
        WebPushKeys.IsEndpoint(endpoint).Should().Be(expected);
    }

    [Fact]
    public void KeyChecks_ShouldMatchTheSubscriptionFormat()
    {
        var (key, publicKey, auth) = NewBrowser();

        WebPushKeys.IsPublicKey(Base64Url.EncodeToString(publicKey)).Should().BeTrue();
        WebPushKeys.IsPublicKey(Base64Url.EncodeToString(publicKey) + "==").Should().BeTrue("padding is tolerated");
        WebPushKeys.IsPublicKey(Base64Url.EncodeToString(publicKey[1..])).Should().BeFalse();
        WebPushKeys.IsAuthSecret(Base64Url.EncodeToString(auth)).Should().BeTrue();
        WebPushKeys.IsAuthSecret("!!").Should().BeFalse();
        WebPushKeys.IsAuthSecret(null).Should().BeFalse();
        key.Dispose();
    }
}
