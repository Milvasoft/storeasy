namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Associated data that binds a sealed secret to its row (see <see cref="ISecretProtector"/>): a sealed value copied to
/// another row or organization does not open.
/// </summary>
public static class SecretScopes
{
    /// <summary>
    /// Scope of an inbound webhook endpoint's secret.
    /// </summary>
    public static string WebhookEndpoint(int organizationId, string publicId) => $"WebhookEndpoint:{organizationId}:{publicId}";

    /// <summary>
    /// Scope of an outgoing webhook's secret.
    /// </summary>
    public static string OrganizationWebhook(int organizationId) => $"OrganizationWebhook:{organizationId}";

    /// <summary>
    /// Associated data of a sealed e-mail outbox payload.
    /// </summary>
    public const string EmailOutbox = "EmailOutbox";
}
