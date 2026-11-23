using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.RemoveTrackedPackage;

/// <summary>
/// Stops tracking a package: the package, its versions and its alerts are deleted. Nothing changes on the registry.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record RemoveTrackedPackageCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package.
    /// </summary>
    public int TrackedPackageId { get; set; }
}
