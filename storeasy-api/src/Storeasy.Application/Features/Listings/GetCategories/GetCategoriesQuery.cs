using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetCategories;

/// <summary>
/// Reads the categories, copyright and content rights declaration of an App Store record live, with the categories App Store Connect offers.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetCategoriesQuery : IQuery<CategoryDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }
}
