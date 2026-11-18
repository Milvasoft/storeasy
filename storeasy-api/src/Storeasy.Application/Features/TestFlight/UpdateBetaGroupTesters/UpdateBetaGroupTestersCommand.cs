using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroupTesters;

/// <summary>
/// Invites testers to a beta group by e-mail and removes testers from it. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record UpdateBetaGroupTestersCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Group.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Testers to invite.
    /// </summary>
    public List<BetaTesterInvite> Add { get; set; } = [];

    /// <summary>
    /// Testers to remove (storeasy ids).
    /// </summary>
    public List<long> Remove { get; set; } = [];
}
