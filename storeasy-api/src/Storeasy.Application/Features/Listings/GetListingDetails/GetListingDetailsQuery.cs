using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListingDetails;

/// <summary>
/// Reads the Google Play app details (contact e-mail, phone, website, default language) live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetListingDetailsQuery : IQuery<ListingDetailsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (Google Play).
    /// </summary>
    public int StoreAppId { get; set; }
}
