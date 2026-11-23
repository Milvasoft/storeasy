using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetPackageSetup;

/// <summary>
/// The trusted publishing wizard of a tracked package: what to fill in on the registry, the CI file to copy and whether the
/// latest release came from the expected repository and workflow. With <see cref="Provider"/> set the wizard is a preview
/// of those values; without it, the saved setup.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPackageSetupQuery : IQuery<TrustedPublishingDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package.
    /// </summary>
    public int TrackedPackageId { get; set; }

    /// <summary>
    /// CI provider of a preview; null reads the saved setup.
    /// </summary>
    public CiProvider? Provider { get; set; }

    /// <summary>
    /// Repository of a preview.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Workflow file of a preview.
    /// </summary>
    public string Workflow { get; set; }

    /// <summary>
    /// CI environment of a preview.
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// Registry user of a preview (NuGet).
    /// </summary>
    public string RegistryUser { get; set; }
}
