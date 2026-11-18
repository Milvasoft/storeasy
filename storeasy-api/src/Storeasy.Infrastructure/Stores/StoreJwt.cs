using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores;

/// <summary>
/// Minimal compact JWS signing for the two store grants: ES256 (App Store Connect, signature in IEEE P1363 form) and
/// RS256 (Google service account). Ported from store-ops <c>signJwt</c>.
/// </summary>
public static class StoreJwt
{
    /// <summary>
    /// App Store Connect token lifetime. Apple accepts at most 20 minutes.
    /// </summary>
    public static readonly TimeSpan AppleTokenLifetime = TimeSpan.FromSeconds(1140);

    /// <summary>
    /// Creates an App Store Connect API token.
    /// </summary>
    public static string CreateAppleToken(AppleCredentials credentials, DateTimeOffset now)
    {
        var header = new Dictionary<string, object> { ["alg"] = "ES256", ["kid"] = credentials.KeyId, ["typ"] = "JWT" };
        var issuedAt = now.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = credentials.IssuerId,
            ["iat"] = issuedAt,
            ["exp"] = issuedAt + (long)AppleTokenLifetime.TotalSeconds,
            ["aud"] = "appstoreconnect-v1",
        };

        using var key = ECDsa.Create();
        key.ImportFromPem(credentials.PrivateKeyPem);

        return Sign(header, claims, data => key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    /// <summary>
    /// Creates the JWT bearer assertion (RFC 7523) a Google service account exchanges for an access token.
    /// </summary>
    public static string CreateGoogleAssertion(GoogleCredentials credentials, string scope, DateTimeOffset now)
    {
        var header = new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" };

        if (!string.IsNullOrEmpty(credentials.PrivateKeyId))
        {
            header["kid"] = credentials.PrivateKeyId;
        }

        var issuedAt = now.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = credentials.ClientEmail,
            ["scope"] = scope,
            ["aud"] = credentials.TokenUri,
            ["iat"] = issuedAt,
            ["exp"] = issuedAt + 3600,
        };

        using var key = RSA.Create();
        key.ImportFromPem(credentials.PrivateKeyPem);

        return Sign(header, claims, data => key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    /// <summary>
    /// Decodes the payload of a compact JWT without verifying it (tests and diagnostics only).
    /// </summary>
    public static JsonElement DecodePayload(string token)
    {
        var parts = token.Split('.');

        return JsonDocument.Parse(Base64UrlDecode(parts[1])).RootElement.Clone();
    }

    /// <summary>
    /// Decodes the header of a compact JWT without verifying it (tests and diagnostics only).
    /// </summary>
    public static JsonElement DecodeHeader(string token)
    {
        var parts = token.Split('.');

        return JsonDocument.Parse(Base64UrlDecode(parts[0])).RootElement.Clone();
    }

    private static string Sign(Dictionary<string, object> header, Dictionary<string, object> claims, Func<byte[], byte[]> signer)
    {
        var signingInput = $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims))}";
        var signature = signer(Encoding.ASCII.GetBytes(signingInput));

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}
