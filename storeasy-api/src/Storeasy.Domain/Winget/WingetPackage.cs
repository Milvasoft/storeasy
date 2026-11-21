using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Winget;

/// <summary>
/// winget side of a store record (<see cref="StoreApp"/> with <see cref="Enums.StoreKind.Winget"/>, whose external id is
/// the package identifier): what the last sync read from the package's manifests in winget-pkgs.
/// </summary>
[Table(TableNames.WingetPackages)]
[Index(nameof(StoreAppId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class WingetPackage : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of a package identifier (manifest schema).
    /// </summary>
    public const int IdentifierMaxLength = 128;

    /// <summary>
    /// Maximum length of a package version (manifest schema).
    /// </summary>
    public const int VersionMaxLength = 128;

    /// <summary>
    /// Maximum length of the publisher and package names (manifest schema).
    /// </summary>
    public const int NameMaxLength = 256;

    /// <summary>
    /// Maximum length of a locale tag (manifest schema).
    /// </summary>
    public const int LocaleMaxLength = 20;

    /// <summary>
    /// Maximum length of <see cref="SyncError"/>.
    /// </summary>
    public const int SyncErrorMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Package identifier (<c>Publisher.Package</c>), as in the manifests (case sensitive).
    /// </summary>
    [MaxLength(IdentifierMaxLength)]
    public string PackageIdentifier { get; set; }

    /// <summary>
    /// Whether winget-pkgs has at least one version of the package; false for a package whose first version is not merged.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Highest version in winget-pkgs (winget's version order).
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string LatestVersion { get; set; }

    /// <summary>
    /// Every version folder in winget-pkgs, highest first.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Versions { get; set; } = [];

    /// <summary>
    /// <c>Publisher</c> of the latest version's default locale.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Publisher { get; set; }

    /// <summary>
    /// <c>PackageName</c> of the latest version's default locale.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string PackageName { get; set; }

    /// <summary>
    /// <c>DefaultLocale</c> of the latest version.
    /// </summary>
    [MaxLength(LocaleMaxLength)]
    public string DefaultLocale { get; set; }

    /// <summary>
    /// Every locale of the latest version (default first).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Locales { get; set; } = [];

    /// <summary>
    /// <c>ManifestVersion</c> (schema version) of the latest version's manifests.
    /// </summary>
    [MaxLength(20)]
    public string ManifestVersion { get; set; }

    /// <summary>
    /// UTC instant of the last successful sync.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Why the last sync could not read the package; null after a successful one.
    /// </summary>
    [MaxLength(SyncErrorMaxLength)]
    public string SyncError { get; set; }

    /// <summary>
    /// The store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
