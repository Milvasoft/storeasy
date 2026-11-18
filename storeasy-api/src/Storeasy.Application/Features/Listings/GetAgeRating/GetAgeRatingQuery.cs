using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetAgeRating;

/// <summary>
/// Reads the age rating declaration of an App Store record live. The new descriptors the API does not have yet (catalog rule apple.age-rating.shared-location-dating) are listed as assisted questions with their deadline.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAgeRatingQuery : IQuery<AgeRatingDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }
}
