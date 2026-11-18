using Storeasy.Application.Utils.Constants;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// Microsoft Entra application credentials of a Partner Center account.
/// </summary>
public sealed class MicrosoftCredentials
{
    /// <summary>
    /// Entra tenant id (GUID).
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; }

    /// <summary>
    /// Application (client) id (GUID).
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; }

    /// <summary>
    /// Client secret value (not the secret id).
    /// </summary>
    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; }

    /// <summary>
    /// Partner Center seller id, sent as <c>X-Seller-Account-Id</c> to the MSI/EXE API.
    /// </summary>
    [JsonPropertyName("sellerId")]
    public string SellerId { get; set; }
}

/// <summary>
/// Parses the pasted Microsoft secret. Returns null and an error key instead of throwing.
/// </summary>
public static partial class MicrosoftCredentialParser
{
    /// <summary>
    /// Longest client secret accepted.
    /// </summary>
    public const int ClientSecretMaxLength = 512;

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidRegex();

    [GeneratedRegex("^[0-9]{1,20}$")]
    private static partial Regex SellerIdRegex();

    /// <summary>
    /// Parses a Microsoft secret <c>{"tenantId","clientId","clientSecret","sellerId"}</c>.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="errorKey"></param>
    /// <returns></returns>
    public static MicrosoftCredentials Parse(string json, out string errorKey)
    {
        errorKey = MessageKey.MsSecretInvalid;

        MicrosoftCredentials credentials;

        try
        {
            credentials = JsonSerializer.Deserialize<MicrosoftCredentials>(json ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }

        if (credentials == null
            || string.IsNullOrWhiteSpace(credentials.TenantId)
            || string.IsNullOrWhiteSpace(credentials.ClientId)
            || string.IsNullOrWhiteSpace(credentials.ClientSecret)
            || string.IsNullOrWhiteSpace(credentials.SellerId))
        {
            return null;
        }

        credentials.TenantId = credentials.TenantId.Trim().ToLowerInvariant();
        credentials.ClientId = credentials.ClientId.Trim().ToLowerInvariant();
        credentials.ClientSecret = credentials.ClientSecret.Trim();
        credentials.SellerId = credentials.SellerId.Trim();

        if (!GuidRegex().IsMatch(credentials.TenantId)
            || !GuidRegex().IsMatch(credentials.ClientId)
            || !SellerIdRegex().IsMatch(credentials.SellerId)
            || credentials.ClientSecret.Length > ClientSecretMaxLength)
        {
            return null;
        }

        errorKey = null;

        return credentials;
    }
}
