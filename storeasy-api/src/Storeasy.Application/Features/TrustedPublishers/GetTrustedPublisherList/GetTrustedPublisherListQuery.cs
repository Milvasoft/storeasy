using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.TrustedPublishers.GetTrustedPublisherList;

/// <summary>
/// Lists the trusted publisher rules of the organization or of one app (design 13.4).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetTrustedPublisherListQuery : IQuery<List<TrustedPublisherDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app.
    /// </summary>
    public int? AppId { get; set; }
}
