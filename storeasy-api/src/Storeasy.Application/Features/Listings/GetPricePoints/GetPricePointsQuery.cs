using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetPricePoints;

/// <summary>
/// Lists the App Store price points of a territory (customer price and proceeds).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPricePointsQuery : IQuery<List<StoreOptionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Territory (App Store code, e.g. TUR); USA when empty.
    /// </summary>
    public string Territory { get; set; }
}
