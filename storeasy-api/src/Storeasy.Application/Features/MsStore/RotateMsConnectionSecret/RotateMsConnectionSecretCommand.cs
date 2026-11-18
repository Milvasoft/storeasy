using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;

namespace Storeasy.Application.Features.MsStore.RotateMsConnectionSecret;

/// <summary>
/// Replaces the client secret of a Microsoft Entra connection before it expires. The new secret must belong to the same
/// tenant, client and seller; it is verified against Microsoft first (token for both APIs and a read call) and only then
/// sealed in place of the old one, with its new expiry date. When verification fails the old secret stays.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
[VerifiedEmailRequired]
public record RotateMsConnectionSecretCommand : ICommand<StoreConnectionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// The new secret JSON <c>{"tenantId","clientId","clientSecret","sellerId"}</c>. Masked in logs.
    /// </summary>
    [LogMasked]
    public string Secret { get; set; }

    /// <summary>
    /// UTC instant the new client secret expires (at most 24 months away).
    /// </summary>
    public DateTime? CredentialExpiryDate { get; set; }
}
