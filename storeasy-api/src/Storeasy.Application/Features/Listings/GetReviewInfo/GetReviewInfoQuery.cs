using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Listings.GetReviewInfo;

/// <summary>
/// Reads the App Review details of the version live and compares them with the app's saved answers. Passwords are never returned.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetReviewInfoQuery : IQuery<ReviewInfoDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }
}
