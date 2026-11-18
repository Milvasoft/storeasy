using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroupBuilds;

/// <summary>
/// Adds builds to a beta group (not expired ones) and removes builds from it. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record UpdateBetaGroupBuildsCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Group.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Builds to add.
    /// </summary>
    public List<long> AddBuildIds { get; set; } = [];

    /// <summary>
    /// Builds to remove.
    /// </summary>
    public List<long> RemoveBuildIds { get; set; } = [];
}
