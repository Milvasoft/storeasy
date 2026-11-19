using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AssetDtos;

namespace Storeasy.Application.Features.Organizations.CreateOrganizationLogoUpload;

/// <summary>
/// Declares a logo file (PNG, JPEG or WebP, at most 2 MB) and returns the presigned PUT URL to upload it with, like
/// <c>assets/upload</c> but for owners. The logo changes only when <c>PUT organizations/logo</c> confirms the upload.
/// </summary>
[OrgAuth(OrgPermission.ManageOrganization)]
public record CreateOrganizationLogoUploadCommand : ICommand<AssetUploadDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// MIME type the upload is sent with: image/png, image/jpeg or image/webp.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Exact size in bytes.
    /// </summary>
    public long Size { get; set; }
}
