using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// The credential of a Firefox Add-ons connection: an AMO API key, made of the JWT issuer (<c>user:12345:67</c>) and the
/// JWT secret, created in the AMO Developer Hub under Tools, Manage API Keys. storeasy signs every request with a short lived
/// HS256 token made from them; the secret never leaves the server.
/// </summary>
/// <param name="Issuer">JWT issuer (<c>user:{userId}:{keyNumber}</c>).</param>
/// <param name="Secret">JWT secret.</param>
public sealed record FirefoxCredentials([property: JsonPropertyName("issuer")] string Issuer,
                                        [property: JsonPropertyName("secret")] string Secret)
{
    /// <summary>
    /// The AMO user id in the issuer (the account the key belongs to).
    /// </summary>
    [JsonIgnore]
    public string UserId => FirefoxCredentialParser.UserIdOf(this.Issuer);
}

/// <summary>
/// Parses <see cref="FirefoxCredentials"/> and signs request tokens.
/// </summary>
public static partial class FirefoxCredentialParser
{
    /// <summary>
    /// Shortest secret accepted (AMO secrets are 64 hexadecimal characters).
    /// </summary>
    public const int SecretMinLength = 32;

    /// <summary>
    /// Parses and normalizes a credential (<c>issuer</c> and <c>secret</c>; <c>jwtIssuer</c> and <c>jwtSecret</c>, the names
    /// the Developer Hub shows, are accepted too). Returns null with <paramref name="errorKey"/> set when it is not valid.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="errorKey"></param>
    /// <returns></returns>
    public static FirefoxCredentials Parse(string json, out string errorKey)
    {
        errorKey = MessageKey.FirefoxSecretInvalid;

        JsonObject root;

        try
        {
            root = JsonNode.Parse(json ?? string.Empty) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (root == null)
            return null;

        var issuer = (Text(root, "issuer") ?? Text(root, "jwtIssuer"))?.Trim();
        var secret = (Text(root, "secret") ?? Text(root, "jwtSecret"))?.Trim();

        if (string.IsNullOrEmpty(issuer) || !IssuerPattern().IsMatch(issuer))
        {
            errorKey = MessageKey.FirefoxIssuerInvalid;
            return null;
        }

        if (string.IsNullOrEmpty(secret) || secret.Length < SecretMinLength || secret.Any(char.IsWhiteSpace))
        {
            errorKey = MessageKey.FirefoxSecretInvalid;
            return null;
        }

        errorKey = null;

        return new FirefoxCredentials(issuer, secret);
    }

    /// <summary>
    /// The user id of an issuer (<c>user:12345:67</c> gives <c>12345</c>), or null.
    /// </summary>
    /// <param name="issuer"></param>
    /// <returns></returns>
    public static string UserIdOf(string issuer)
    {
        var match = IssuerPattern().Match(issuer ?? string.Empty);

        return match.Success ? match.Groups["user"].Value : null;
    }

    /// <summary>
    /// A request token: HS256 over <c>iss</c>, a random <c>jti</c>, <c>iat</c> and <c>exp</c> (at most 300 seconds later).
    /// </summary>
    /// <param name="credentials"></param>
    /// <param name="now"></param>
    /// <param name="lifetimeSeconds"></param>
    /// <returns></returns>
    public static string CreateToken(FirefoxCredentials credentials, DateTimeOffset now, int lifetimeSeconds = 60)
    {
        var issuedAt = now.ToUnixTimeSeconds();
        var header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"u8.ToArray());
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = credentials.Issuer,
            ["jti"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            ["iat"] = issuedAt,
            ["exp"] = issuedAt + Math.Clamp(lifetimeSeconds, 10, 300),
        }));
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(credentials.Secret), Encoding.ASCII.GetBytes($"{header}.{payload}"));

        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Text(JsonObject root, string name)
        => root.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"^user:(?<user>\d{1,12}):\d{1,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex IssuerPattern();
}
