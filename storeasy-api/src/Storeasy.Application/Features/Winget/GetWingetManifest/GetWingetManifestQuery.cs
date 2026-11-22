using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.WingetDtos;

namespace Storeasy.Application.Features.Winget.GetWingetManifest;

/// <summary>
/// Reads the manifests of one version of a winget package live from winget-pkgs: the listing texts per locale, the
/// installers, the YAML files and storeasy's schema and policy checks.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetWingetManifestQuery : IQuery<WingetManifestDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// winget store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Version folder to read; null reads the latest.
    /// </summary>
    public string Version { get; set; }
}
