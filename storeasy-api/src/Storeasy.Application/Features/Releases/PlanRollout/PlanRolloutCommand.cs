using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.PlanRollout;

/// <summary>
/// Plans a rollout change (PUT releases/rollout): Play staged rollout (fraction only grows; halt, resume, complete) or App Store phased release (pause, resume, complete). Production and App Store rollouts need ReleaseProduction to apply.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record PlanRolloutCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// App Store version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Play track (production by default).
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// What to do.
    /// </summary>
    public RolloutAction Action { get; set; }

    /// <summary>
    /// New Play rollout percentage (0-100) for SetPercentage.
    /// </summary>
    public double? Percentage { get; set; }

    /// <summary>
    /// Why (kept in the audit log).
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
