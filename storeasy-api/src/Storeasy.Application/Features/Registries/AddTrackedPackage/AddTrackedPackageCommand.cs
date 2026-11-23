using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.AddTrackedPackage;

/// <summary>
/// Starts tracking a package of a registry by its name. The name is checked against the registry's rules and the registry's
/// public API (the package must exist); the package is saved with the registry's spelling and its first sync is queued.
/// No credential is asked or stored.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record AddTrackedPackageCommand : ICommand<TrackedPackageAddedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The registry.
    /// </summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>
    /// Package name (npm: <c>name</c> or <c>@scope/name</c>; NuGet: package id; PyPI: project name; crates.io: crate name).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// storeasy app to link the package to.
    /// </summary>
    public int? AppId { get; set; }
}
