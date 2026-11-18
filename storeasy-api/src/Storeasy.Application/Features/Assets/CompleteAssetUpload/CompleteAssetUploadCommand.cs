using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AssetDtos;

namespace Storeasy.Application.Features.Assets.CompleteAssetUpload;

/// <summary>
/// Confirms an upload: completes a multipart upload with the parts' ETags, then checks that the stored object has the
/// declared size. A mismatching object is deleted and the asset rejected. Calling it again for a confirmed asset returns
/// the asset.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CompleteAssetUploadCommand : ICommand<AssetDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Asset id from the upload request.
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Uploaded parts of a multipart upload (part number and the ETag of its PUT response). Empty for a single upload.
    /// </summary>
    public List<UploadedPart> Parts { get; set; } = [];
}
