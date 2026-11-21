using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AsoExperimentDtos;

namespace Storeasy.Application.Features.AsoExperiments.UpdateAsoExperimentChecklist;

/// <summary>
/// Saves the done items of a Play record's assisted checklist (the Play Console steps). Editors and above.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateAsoExperimentChecklistCommand : ICommand<AsoExperimentDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Done items (1 based).
    /// </summary>
    public List<int> DoneItems { get; set; } = [];
}
