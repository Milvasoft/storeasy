using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.AsoExperiments.DeleteAsoExperiment;

/// <summary>
/// Deletes a record from storeasy: a draft never written to a store, or a Play record. Nothing is deleted in a store.
/// Editors and above.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record DeleteAsoExperimentCommand : ICommand<bool>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record id.
    /// </summary>
    public int Id { get; set; }
}
