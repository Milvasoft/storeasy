using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Apps;

/// <summary>
/// A build of a store record as the store reports it: an App Store Connect build, a Play bundle (by version code), later a
/// Microsoft Store package. <see cref="Version"/> is the user facing version, <see cref="BuildNumber"/> the build number
/// or version code.
/// </summary>
[Table(TableNames.Builds)]
[Index(nameof(StoreAppId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class Build : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the build (Apple build id, Play version code).
    /// </summary>
    [MaxLength(StoreApp.ExternalIdMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// User facing version (1.4.0). Null when the store does not say.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Version { get; set; }

    /// <summary>
    /// Build number (CFBundleVersion) or version code.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string BuildNumber { get; set; }

    /// <summary>
    /// Processing state.
    /// </summary>
    public BuildProcessingState ProcessingState { get; set; }

    /// <summary>
    /// Tracks the build is released to (Play), comma separated.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Tracks { get; set; }

    /// <summary>
    /// Minimum OS version, when the store reports it.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string MinOsVersion { get; set; }

    /// <summary>
    /// UTC instant the build was uploaded.
    /// </summary>
    public DateTime? UploadedDate { get; set; }

    /// <summary>
    /// UTC instant the build stops being testable (TestFlight).
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant of the last sync that saw the build.
    /// </summary>
    public DateTime SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
