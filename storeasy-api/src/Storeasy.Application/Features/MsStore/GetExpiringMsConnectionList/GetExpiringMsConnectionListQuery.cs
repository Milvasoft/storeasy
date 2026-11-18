using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetExpiringMsConnectionList;

/// <summary>
/// Lists the organization's Microsoft Entra connections whose client secret expires within <see cref="Days"/> days
/// (expired ones included), soonest first. The same query across organizations is
/// <see cref="IMsConnectionExpiryReader"/>, for the reminder job.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record GetExpiringMsConnectionListQuery : IQuery<List<MsExpiringConnectionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Window in days (default 30, the reminder lead time).
    /// </summary>
    public int Days { get; set; } = Domain.MsStore.MsSecretRules.WarnDaysBefore;
}
