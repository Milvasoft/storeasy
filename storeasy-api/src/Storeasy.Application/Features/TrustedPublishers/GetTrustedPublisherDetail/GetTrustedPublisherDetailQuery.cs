using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.TrustedPublishers.GetTrustedPublisherDetail;

/// <summary>
/// Reads one trusted publisher rule with the claims of its last exchange (design 13.7 diagnosis drawer).
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record GetTrustedPublisherDetailQuery : IQuery<TrustedPublisherDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Rule id.
    /// </summary>
    public int Id { get; set; }
}
