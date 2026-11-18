using Storeasy.Application.Interfaces;
using Storeasy.Domain.JsonModels;
using Storeasy.Infrastructure.Stores.Microsoft;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores;

/// <summary>
/// Microsoft Store side of the store gateway: offline inspection of the Entra secret. Verification and reads delegate to
/// <see cref="IMicrosoftStoreClient"/>.
/// </summary>
public sealed partial class StoreGateway
{
    /// <summary>
    /// Parses a Microsoft secret and returns its fingerprint: tenant and client id suffixes and the seller id. The expiry
    /// date is not part of the secret; the caller adds it.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <returns></returns>
    private static StoreSecretInspection InspectMicrosoft(string secretJson)
    {
        var credentials = MicrosoftCredentialParser.Parse(secretJson, out var errorKey);

        if (credentials == null)
            return new StoreSecretInspection(false, errorKey, null, null);

        var fingerprint = new ConnectionFingerprint
        {
            TenantIdSuffix = credentials.TenantId[^4..],
            ClientIdSuffix = credentials.ClientId[^4..],
            SellerId = credentials.SellerId,
        };

        return new StoreSecretInspection(true, null, fingerprint, JsonSerializer.Serialize(credentials));
    }
}
