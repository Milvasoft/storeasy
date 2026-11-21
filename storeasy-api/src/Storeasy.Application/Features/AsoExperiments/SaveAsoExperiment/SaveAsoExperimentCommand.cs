using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AsoExperimentDtos;

namespace Storeasy.Application.Features.AsoExperiments.SaveAsoExperiment;

/// <summary>
/// Creates an experiment or custom page record (a draft in storeasy; nothing goes to the store until a plan of it is
/// approved), or edits one while it is still editable. Editors and above.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SaveAsoExperimentCommand : ICommand<AsoExperimentDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record to edit; null creates one.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record (it decides the store). Ignored when editing.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Kind. Ignored when editing.
    /// </summary>
    public AsoExperimentKind Kind { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Traffic proportion in percent (product page optimization and Play experiments).
    /// </summary>
    public int? TrafficProportion { get; set; }

    /// <summary>
    /// Variants with what they change. The store ids of existing variants are kept by key.
    /// </summary>
    public List<AsoExperimentVariant> Variants { get; set; } = [];

    /// <summary>
    /// Free note.
    /// </summary>
    public string Note { get; set; }
}
