using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListingByField;

/// <summary>
/// Reads one listing field in every locale of a store record (e.g. the keywords of every language).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetListingByFieldQuery : IQuery<List<ListingFieldDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Field name (keywords, subtitle, title...).
    /// </summary>
    public string Field { get; set; }
}
