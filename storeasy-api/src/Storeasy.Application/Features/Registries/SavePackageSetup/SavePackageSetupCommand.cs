using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.SavePackageSetup;

/// <summary>
/// Saves the trusted publishing setup of a tracked package (the repository, workflow and environment releases should come
/// from), or clears it with a null <see cref="Provider"/>. Every sync then checks the latest release against it and raises a
/// publisher mismatch alert. Nothing is written to the registry or the repository.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SavePackageSetupCommand : ICommand<TrustedPublishingDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package.
    /// </summary>
    public int TrackedPackageId { get; set; }

    /// <summary>
    /// CI provider; null clears the setup.
    /// </summary>
    public CiProvider? Provider { get; set; }

    /// <summary>
    /// Repository: <c>owner/repo</c> on GitHub, <c>namespace/project</c> on GitLab.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Workflow file name in <c>.github/workflows</c> (GitHub) or the top-level CI file path (GitLab).
    /// </summary>
    public string Workflow { get; set; }

    /// <summary>
    /// CI environment; optional.
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// Registry user the CI logs in as (NuGet's <c>NuGet/login</c> action); optional.
    /// </summary>
    public string RegistryUser { get; set; }
}
