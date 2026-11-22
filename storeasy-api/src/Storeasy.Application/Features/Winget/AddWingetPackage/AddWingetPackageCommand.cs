using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.WingetDtos;

namespace Storeasy.Application.Features.Winget.AddWingetPackage;

/// <summary>
/// Adds a winget package by its package identifier (winget has no API that lists a publisher's packages). The package is
/// attached to an existing storeasy app (usually the one of the same program's Microsoft Store MSI/EXE product) or to a
/// new app, and a sync reads its manifests. A package without a version in winget-pkgs yet can be added too: its first
/// version is then a "new package" pull request.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record AddWingetPackageCommand : ICommand<WingetPackageAddedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// winget connection.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Package identifier (<c>Publisher.Package</c>, case sensitive as in winget-pkgs).
    /// </summary>
    public string PackageIdentifier { get; set; }

    /// <summary>
    /// storeasy app to attach the package to; null creates a new app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Name of a new app; defaults to the package identifier.
    /// </summary>
    public string Name { get; set; }
}
