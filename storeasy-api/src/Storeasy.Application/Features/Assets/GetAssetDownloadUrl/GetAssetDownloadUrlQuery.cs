using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AssetDtos;

namespace Storeasy.Application.Features.Assets.GetAssetDownloadUrl;

/// <summary>
/// Returns a short lived presigned download URL of a confirmed file of the organization.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAssetDownloadUrlQuery : IQuery<AssetDownloadDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Asset id.
    /// </summary>
    public long AssetId { get; set; }
}
