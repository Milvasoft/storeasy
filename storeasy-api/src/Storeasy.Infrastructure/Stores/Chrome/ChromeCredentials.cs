using Storeasy.Application.Utils.Chrome;
using Storeasy.Application.Utils.Constants;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// The credential of a Chrome Web Store connection: the publisher id and one way to get an access token for the
/// <c>chromewebstore</c> scope, either a Google service account JSON key (the service account email is added under
/// Account in the developer dashboard) or an OAuth client with a refresh token of the Google account that owns the items.
/// <see cref="ItemId"/> is an optional extension id the verification reads, since the API has no call that reads a
/// publisher.
/// </summary>
/// <param name="PublisherId">Publisher id (developer dashboard, Account).</param>
/// <param name="ServiceAccountKey">The service account JSON key, as text; null for an OAuth client.</param>
/// <param name="ClientId">OAuth client id.</param>
/// <param name="ClientSecret">OAuth client secret.</param>
/// <param name="RefreshToken">OAuth refresh token.</param>
/// <param name="ItemId">Extension id the verification reads, optional.</param>
public sealed record ChromeCredentials([property: JsonPropertyName("publisherId")] string PublisherId,
                                       [property: JsonPropertyName("serviceAccountKey")] string ServiceAccountKey,
                                       [property: JsonPropertyName("clientId")] string ClientId,
                                       [property: JsonPropertyName("clientSecret")] string ClientSecret,
                                       [property: JsonPropertyName("refreshToken")] string RefreshToken,
                                       [property: JsonPropertyName("itemId")] string ItemId)
{
    /// <summary>
    /// Whether the token comes from a service account key.
    /// </summary>
    [JsonIgnore]
    public bool IsServiceAccount => this.ServiceAccountKey != null;

    /// <summary>
    /// The parsed service account key; null for an OAuth client.
    /// </summary>
    [JsonIgnore]
    public GoogleCredentials ServiceAccount => IsServiceAccount ? StoreCredentialParser.ParseGoogle(this.ServiceAccountKey, out _) : null;
}

/// <summary>
/// Parses <see cref="ChromeCredentials"/>.
/// </summary>
public static class ChromeCredentialParser
{
    /// <summary>
    /// Parses and normalizes a Chrome credential. The service account key may be given as a JSON object or as its text;
    /// it is kept as text. Returns null with <paramref name="errorKey"/> set when it is not valid.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="errorKey"></param>
    /// <returns></returns>
    public static ChromeCredentials Parse(string json, out string errorKey)
    {
        errorKey = MessageKey.ChromeSecretInvalid;

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

        var publisherId = Text(root, "publisherId")?.Trim();

        if (string.IsNullOrEmpty(publisherId))
            return null;

        if (!ChromeVersions.IsPublisherId(publisherId))
        {
            errorKey = MessageKey.ChromePublisherIdInvalid;
            return null;
        }

        var itemId = Text(root, "itemId")?.Trim();

        if (string.IsNullOrEmpty(itemId))
        {
            itemId = null;
        }
        else if (!ChromeVersions.IsItemId(itemId))
        {
            errorKey = MessageKey.ChromeItemIdInvalid;
            return null;
        }

        var key = root["serviceAccountKey"];

        if (key != null && !(key is JsonValue value && value.TryGetValue<string>(out var empty) && string.IsNullOrWhiteSpace(empty)))
        {
            var keyText = key is JsonObject ? key.ToJsonString() : Text(root, "serviceAccountKey");

            if (keyText == null || StoreCredentialParser.ParseGoogle(keyText, out _) == null)
            {
                errorKey = MessageKey.ChromeServiceAccountInvalid;
                return null;
            }

            errorKey = null;

            return new ChromeCredentials(publisherId, keyText, null, null, null, itemId);
        }

        var clientId = Text(root, "clientId")?.Trim();
        var clientSecret = Text(root, "clientSecret")?.Trim();
        var refreshToken = Text(root, "refreshToken")?.Trim();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
        {
            errorKey = MessageKey.ChromeOAuthInvalid;
            return null;
        }

        errorKey = null;

        return new ChromeCredentials(publisherId, null, clientId, clientSecret, refreshToken, itemId);
    }

    /// <summary>
    /// Last characters of an OAuth client id, for the fingerprint (client ids end in
    /// <c>.apps.googleusercontent.com</c>, so the part before it is used).
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public static string ClientIdSuffix(string clientId)
    {
        var id = clientId?.Split('.')[0] ?? string.Empty;

        return id.Length <= 6 ? id : id[^6..];
    }

    /// <summary>
    /// Cache key of a credential's access token (a hash, so the secret is not kept as a key).
    /// </summary>
    /// <param name="credentials"></param>
    /// <returns></returns>
    public static string CacheKey(ChromeCredentials credentials)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(credentials.ServiceAccountKey ?? $"{credentials.ClientId}\n{credentials.ClientSecret}\n{credentials.RefreshToken}")));

    private static string Text(JsonObject root, string name)
        => root.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
}
