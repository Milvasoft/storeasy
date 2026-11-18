using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetMsInstallerPackageList;

/// <summary>
/// Lists the installer packages of an MSI/EXE product with storeasy's Authenticode check result. Database only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMsInstallerPackageListQuery : IQuery<List<MsInstallerPackageDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
