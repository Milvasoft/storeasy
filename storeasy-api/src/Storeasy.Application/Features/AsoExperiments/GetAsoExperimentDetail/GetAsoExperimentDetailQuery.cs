using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoExperimentDtos;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentDetail;

/// <summary>
/// One experiment or custom page record with its variants, links, timeline, the writes that can be planned now and, for
/// Play, the assisted checklist. Members (View).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoExperimentDetailQuery : IQuery<AsoExperimentDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record id.
    /// </summary>
    public int Id { get; set; }
}
