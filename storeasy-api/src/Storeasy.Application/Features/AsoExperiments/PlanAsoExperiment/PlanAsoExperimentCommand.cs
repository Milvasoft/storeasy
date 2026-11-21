using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.AsoExperiments.PlanAsoExperiment;

/// <summary>
/// Plans a store write of an App Store experiment or custom product page: writing it (create or update with treatments,
/// localizations, screenshots and previews), submitting it for review, starting or stopping it, or writing the winning
/// treatment to the app version. Made from a live read; approved like every plan, with the operation's permission
/// (edit content, submit for review, or production release) and, except for writing a draft, a typed confirmation.
/// Editors and above make it (dry run by default).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanAsoExperimentCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// What to plan.
    /// </summary>
    public AsoExperimentOperation Operation { get; set; }

    /// <summary>
    /// Submit: add it to the open review submission so it goes with the app version, instead of submitting it alone.
    /// </summary>
    public bool WithVersion { get; set; }

    /// <summary>
    /// Apply winner: the winning variant (null: the recorded winner); it is recorded as the winner.
    /// </summary>
    public string WinnerVariantKey { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Typed confirmation, when approving right away a plan that needs one.
    /// </summary>
    public string Confirm { get; set; }
}
