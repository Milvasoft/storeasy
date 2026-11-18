using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Events;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// Opens the secrets of an inbound endpoint: the current one, and the previous one while its grace period lasts.
/// </summary>
public static class WebhookEndpointSecrets
{
    /// <summary>
    /// The secrets a signature may be checked against, current first. A secret that does not open is skipped.
    /// </summary>
    public static List<string> Valid(ISecretProtector protector, WebhookEndpoint endpoint, DateTime now)
    {
        var scope = SecretScopes.WebhookEndpoint(endpoint.OrganizationId, endpoint.PublicId);
        var secrets = new List<string>();

        Add(secrets, protector, endpoint.EncryptedSecret, endpoint.EncryptedDataKey, endpoint.KeyVersion, scope);

        if (endpoint.PreviousSecretValidUntil > now && endpoint.PreviousKeyVersion.HasValue)
            Add(secrets, protector, endpoint.PreviousEncryptedSecret, endpoint.PreviousEncryptedDataKey, endpoint.PreviousKeyVersion.Value, scope);

        return secrets;
    }

    /// <summary>
    /// The current secret, or null when it does not open.
    /// </summary>
    public static string Current(ISecretProtector protector, WebhookEndpoint endpoint)
    {
        var secrets = new List<string>();

        Add(secrets, protector, endpoint.EncryptedSecret, endpoint.EncryptedDataKey, endpoint.KeyVersion, SecretScopes.WebhookEndpoint(endpoint.OrganizationId, endpoint.PublicId));

        return secrets.FirstOrDefault();
    }

    private static void Add(List<string> secrets, ISecretProtector protector, byte[] secret, byte[] dataKey, int keyVersion, string scope)
    {
        if (secret == null || secret.Length == 0 || dataKey == null || dataKey.Length == 0)
            return;

        try
        {
            secrets.Add(Encoding.UTF8.GetString(protector.Unprotect(new ProtectedSecret(secret, dataKey, keyVersion), scope)));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // A secret sealed with a retired master key or for another row never verifies anything.
        }
    }
}
