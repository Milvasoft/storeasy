using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Builds.GetBuildList;

/// <summary>
/// Pages through the synced builds of a store record (App Store builds with their processing state and export compliance; Play bundles with their tracks), newest first.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetBuildListQuery : ListRequest, IListRequestQuery<BuildDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
