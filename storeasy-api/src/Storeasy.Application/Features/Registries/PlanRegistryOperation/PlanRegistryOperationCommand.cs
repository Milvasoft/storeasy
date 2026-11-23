using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.PlanRegistryOperation;

/// <summary>
/// Plans a registry write operation on a version (always a dry run): NuGet unlist or relist, npm deprecate, undeprecate or
/// dist-tag, crates.io yank or unyank, RubyGems yank. It needs the registry's API key saved; approving it (typed
/// confirmation, production release permission) performs it with that key.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanRegistryOperationCommand : ICommand<RegistryPlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Package.
    /// </summary>
    public int TrackedPackageId { get; set; }

    /// <summary>
    /// Operation (not <see cref="RegistryPlanKind.Release"/>).
    /// </summary>
    public RegistryPlanKind Kind { get; set; }

    /// <summary>
    /// Version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Deprecation message, or the dist-tag name.
    /// </summary>
    public string Argument { get; set; }
}
