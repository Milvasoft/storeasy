using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Assets;

/// <summary>
/// A file of an organization in the private bucket (screenshots, graphics, mockup outputs, build artifacts). The row is
/// created when upload URLs are handed out (<see cref="AssetStatus.Pending"/>) and confirmed after the storage reports the
/// object with the declared size (<see cref="AssetStatus.Uploaded"/>). Reads go through presigned URLs only.
/// </summary>
[Table(TableNames.Assets)]
[Index(nameof(OrganizationId), nameof(Purpose))]
[Index(nameof(ObjectKey), IsUnique = true)]
public class Asset : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="FileName"/>.
    /// </summary>
    public const int FileNameMaxLength = 255;

    /// <summary>
    /// Column length of <see cref="ContentType"/>.
    /// </summary>
    public const int ContentTypeMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// What the file is for.
    /// </summary>
    public AssetPurpose Purpose { get; set; }

    /// <summary>
    /// Object key in the bucket.
    /// </summary>
    [MaxLength(512)]
    public string ObjectKey { get; set; }

    /// <summary>
    /// Original file name as uploaded.
    /// </summary>
    [MaxLength(FileNameMaxLength)]
    public string FileName { get; set; }

    /// <summary>
    /// MIME type.
    /// </summary>
    [MaxLength(ContentTypeMaxLength)]
    public string ContentType { get; set; }

    /// <summary>
    /// Size in bytes: declared before the upload, checked against the stored object.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the content as declared by the uploader (dedupe and later integrity checks).
    /// </summary>
    [MaxLength(64)]
    public string Sha256 { get; set; }

    /// <summary>
    /// Upload state.
    /// </summary>
    public AssetStatus Status { get; set; } = AssetStatus.Pending;

    /// <summary>
    /// Multipart upload id while a multipart upload is open. Null for a single PUT.
    /// </summary>
    [MaxLength(512)]
    public string UploadId { get; set; }

    /// <summary>
    /// UTC instant the upload was confirmed.
    /// </summary>
    public DateTime? UploadDate { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
