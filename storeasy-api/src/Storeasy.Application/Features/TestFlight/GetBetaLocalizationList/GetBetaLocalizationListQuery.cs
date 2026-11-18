using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.TestFlightDtos;

namespace Storeasy.Application.Features.TestFlight.GetBetaLocalizationList;

/// <summary>
/// The beta app descriptions and feedback e-mails of an App Store record, per language.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetBetaLocalizationListQuery : IQuery<List<BetaLocalizationDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
