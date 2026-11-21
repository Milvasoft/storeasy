using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoExperimentDtos;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentOverview;

/// <summary>
/// The experiments tab of an app: its experiment and custom page records (newest first) and what each of its store
/// records supports. Members (View).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoExperimentOverviewQuery : IQuery<AsoExperimentOverviewDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }
}
