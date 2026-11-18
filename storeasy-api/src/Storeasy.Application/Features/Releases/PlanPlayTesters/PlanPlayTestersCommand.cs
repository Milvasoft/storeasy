using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.PlanPlayTesters;

/// <summary>
/// Plans the tester Google Groups of a Play testing track (PUT releases/testers).
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record PlanPlayTestersCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (Google Play).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Testing track (not production).
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Google Group addresses.
    /// </summary>
    public List<string> GoogleGroups { get; set; } = [];

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
