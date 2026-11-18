using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Assets;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.AssetDtos;

/// <summary>
/// A stored file of the organization (never its URL; ask for a download URL).
/// </summary>
public class AssetDto
{
    /// <summary>
    /// Asset id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// What the file is for.
    /// </summary>
    public AssetPurpose Purpose { get; set; }

    /// <summary>
    /// Original file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// MIME type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Declared SHA-256 (hex).
    /// </summary>
    public string Sha256 { get; set; }

    /// <summary>
    /// Upload state.
    /// </summary>
    public AssetStatus Status { get; set; }

    /// <summary>
    /// UTC instant the upload started.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant the upload was confirmed.
    /// </summary>
    public DateTime? UploadDate { get; set; }

    /// <summary>
    /// Projection from an asset row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<Asset, AssetDto>> Projection { get; } = a => new AssetDto
    {
        Id = a.Id,
        Purpose = a.Purpose,
        FileName = a.FileName,
        ContentType = a.ContentType,
        Size = a.Size,
        Sha256 = a.Sha256,
        Status = a.Status,
        CreationDate = a.CreationDate,
        UploadDate = a.UploadDate,
    };
}
