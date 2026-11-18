using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AppDtos;

namespace Storeasy.Application.Features.Apps.GetAppDetail;

/// <summary>
/// Reads one app of the organization with its store records and newest builds.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAppDetailQuery : IQuery<AppDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }
}
