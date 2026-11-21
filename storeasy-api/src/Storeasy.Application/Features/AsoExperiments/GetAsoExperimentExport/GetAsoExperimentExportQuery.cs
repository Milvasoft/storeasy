using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoExperimentDtos;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentExport;

/// <summary>
/// The variants of a record ready to be set up by hand in a store console (Play experiments and custom listings): short
/// lived download links of every file and the localized texts. Members (View).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoExperimentExportQuery : IQuery<AsoExperimentExportDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Record id.
    /// </summary>
    public int Id { get; set; }
}
