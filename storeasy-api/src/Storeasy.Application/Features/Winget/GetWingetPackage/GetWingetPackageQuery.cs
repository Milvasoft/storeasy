using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.WingetDtos;

namespace Storeasy.Application.Features.Winget.GetWingetPackage;

/// <summary>
/// Reads a winget package as the last sync saw it, with its pull requests and review states and the installers storeasy
/// knows for the same app. Database only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetWingetPackageQuery : IQuery<WingetPackageDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// winget store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
