using Storeasy.Application.Utils.Constants;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Storeasy.Infrastructure.Stores;

/// <summary>
/// App Store Connect API key.
/// </summary>
/// <param name="IssuerId">Issuer id (UUID) shown above the keys table.</param>
/// <param name="KeyId">10 character key id.</param>
/// <param name="PrivateKeyPem">Content of the AuthKey_*.p8 file.</param>
public sealed record AppleCredentials(
    [property: JsonPropertyName("issuerId")] string IssuerId,
    [property: JsonPropertyName("keyId")] string KeyId,
    [property: JsonPropertyName("privateKey")] string PrivateKeyPem);

/// <summary>
/// Google service account key, the fields storeasy uses.
/// </summary>
/// <param name="ClientEmail">Service account e-mail.</param>
/// <param name="PrivateKeyPem">RSA private key.</param>
/// <param name="PrivateKeyId">Key id, sent as the JWT kid.</param>
/// <param name="TokenUri">OAuth token endpoint.</param>
/// <param name="ProjectId">Google Cloud project id.</param>
public sealed record GoogleCredentials(string ClientEmail, string PrivateKeyPem, string PrivateKeyId, string TokenUri, string ProjectId);

/// <summary>
/// Parses the pasted secrets. Each method returns null and an error key instead of throwing, so the caller can answer
/// with a validation message.
/// </summary>
public static partial class StoreCredentialParser
{
    /// <summary>Message key: the Apple secret is not valid JSON with the three fields.</summary>
    public const string AppleSecretInvalid = MessageKey.AppleSecretInvalid;

    /// <summary>Message key: the Apple private key is not an EC P-256 key.</summary>
    public const string AppleKeyInvalid = MessageKey.AppleKeyInvalid;

    /// <summary>Message key: the Google secret is not a service account key file.</summary>
    public const string GoogleSecretInvalid = MessageKey.GoogleSecretInvalid;

    /// <summary>Message key: the Google private key is not an RSA key.</summary>
    public const string GoogleKeyInvalid = MessageKey.GoogleKeyInvalid;

    private const string _defaultTokenUri = "https://oauth2.googleapis.com/token";

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex IssuerIdRegex();

    [GeneratedRegex("^[A-Z0-9]{10}$")]
    private static partial Regex KeyIdRegex();

    /// <summary>
    /// Parses an Apple secret.
    /// </summary>
    public static AppleCredentials ParseApple(string json, out string errorKey)
    {
        errorKey = AppleSecretInvalid;

        AppleCredentials credentials;

        try
        {
            credentials = JsonSerializer.Deserialize<AppleCredentials>(json ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }

        if (credentials == null
            || string.IsNullOrWhiteSpace(credentials.IssuerId)
            || string.IsNullOrWhiteSpace(credentials.KeyId)
            || string.IsNullOrWhiteSpace(credentials.PrivateKeyPem))
        {
            return null;
        }

        credentials = credentials with { IssuerId = credentials.IssuerId.Trim(), KeyId = credentials.KeyId.Trim().ToUpperInvariant() };

        if (!IssuerIdRegex().IsMatch(credentials.IssuerId) || !KeyIdRegex().IsMatch(credentials.KeyId))
        {
            return null;
        }

        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(credentials.PrivateKeyPem);

            if (key.KeySize != 256)
            {
                errorKey = AppleKeyInvalid;
                return null;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            errorKey = AppleKeyInvalid;
            return null;
        }

        errorKey = null;

        return credentials;
    }

    /// <summary>
    /// Parses a Google service account key file.
    /// </summary>
    public static GoogleCredentials ParseGoogle(string json, out string errorKey)
    {
        errorKey = GoogleSecretInvalid;

        JsonElement root;

        try
        {
            root = JsonDocument.Parse(json ?? string.Empty).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object
            || ReadString(root, "type") != "service_account"
            || string.IsNullOrWhiteSpace(ReadString(root, "client_email"))
            || string.IsNullOrWhiteSpace(ReadString(root, "private_key")))
        {
            return null;
        }

        var credentials = new GoogleCredentials(ReadString(root, "client_email").Trim(),
                                                ReadString(root, "private_key"),
                                                ReadString(root, "private_key_id"),
                                                ReadString(root, "token_uri") ?? _defaultTokenUri,
                                                ReadString(root, "project_id"));

        if (!Uri.TryCreate(credentials.TokenUri, UriKind.Absolute, out var tokenUri)
            || tokenUri.Scheme != Uri.UriSchemeHttps
            || !tokenUri.Host.EndsWith("googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var key = RSA.Create();
            key.ImportFromPem(credentials.PrivateKeyPem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            errorKey = GoogleKeyInvalid;
            return null;
        }

        errorKey = null;

        return credentials;
    }

    private static string ReadString(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
