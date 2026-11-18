using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListing;

/// <summary>
/// Reads the listing texts of a store record live from the store, with the catalog limits of every field.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetListingQuery : IQuery<ListingDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Locale of the fields; every locale when empty.
    /// </summary>
    public string Locale { get; set; }
}
