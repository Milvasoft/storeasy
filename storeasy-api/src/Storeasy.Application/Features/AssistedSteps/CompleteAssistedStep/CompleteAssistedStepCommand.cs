using Milvasoft.Components.CQRS.Command;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.CompleteAssistedStep;

/// <summary>
/// Records the progress or result of an assisted step: checklist items done, and Done or Skipped with an optional evidence screenshot (an uploaded asset) and note. Done and Skipped steps turn the readiness item and the timeline entry done. Returns the step id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CompleteAssistedStepCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Step id.
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// InProgress, Done or Skipped.
    /// </summary>
    public AssistedStepStatus Status { get; set; } = AssistedStepStatus.Done;

    /// <summary>
    /// Checklist positions (from 1) that are done; null keeps them.
    /// </summary>
    public List<int> DoneItems { get; set; }

    /// <summary>
    /// Evidence screenshot (uploaded asset).
    /// </summary>
    public long? EvidenceAssetId { get; set; }

    /// <summary>
    /// Note on the result.
    /// </summary>
    public string Note { get; set; }
}
