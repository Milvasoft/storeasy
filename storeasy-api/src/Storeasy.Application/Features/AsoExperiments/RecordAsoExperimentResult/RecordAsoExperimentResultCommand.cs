using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AsoExperimentDtos;

namespace Storeasy.Application.Features.AsoExperiments.RecordAsoExperimentResult;

/// <summary>
/// Records what happened to an experiment outside storeasy's reach: for Play records the state, start and end the member
/// saw in Play Console; for every record the result summary, the winner and a link to the results (App Store results are
/// only shown in App Store Connect). Values left null are kept. Editors and above.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record RecordAsoExperimentResultCommand : ICommand<AsoExperimentDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Play records: Running, Stopped or Completed. App Store records take their state from the store.
    /// </summary>
    public AsoExperimentStatus? Status { get; set; }

    /// <summary>
    /// Play records: start (UTC).
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Play records: end (UTC).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Winning variant key.
    /// </summary>
    public string WinnerVariantKey { get; set; }

    /// <summary>
    /// Result in the member's words.
    /// </summary>
    public string ResultSummary { get; set; }

    /// <summary>
    /// Link to the results (https).
    /// </summary>
    public string ResultUrl { get; set; }
}
