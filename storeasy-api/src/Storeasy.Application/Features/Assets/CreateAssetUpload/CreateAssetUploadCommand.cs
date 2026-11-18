using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AssetDtos;

namespace Storeasy.Application.Features.Assets.CreateAssetUpload;

/// <summary>
/// Declares a file to upload and returns presigned upload URLs: one PUT for a small file, one per part for a large one
/// (over 64 MB). The file counts only after the complete call confirms it with the declared size.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CreateAssetUploadCommand : ICommand<AssetUploadDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// What the file is for (decides the size limit).
    /// </summary>
    public AssetPurpose Purpose { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// MIME type the upload is sent with.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Exact size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the content (optional).
    /// </summary>
    public string Sha256 { get; set; }
}
