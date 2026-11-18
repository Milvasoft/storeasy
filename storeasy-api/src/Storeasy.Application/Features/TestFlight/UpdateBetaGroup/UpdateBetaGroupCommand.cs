using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroup;

/// <summary>
/// Renames a beta group or changes its public link (on/off, limit). Null fields stay. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record UpdateBetaGroupCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Group.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// New name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Public link on or off.
    /// </summary>
    public bool? PublicLinkEnabled { get; set; }

    /// <summary>
    /// Public link limit.
    /// </summary>
    public int? PublicLinkLimit { get; set; }

    /// <summary>
    /// Limit on or off.
    /// </summary>
    public bool? PublicLinkLimitEnabled { get; set; }
}
