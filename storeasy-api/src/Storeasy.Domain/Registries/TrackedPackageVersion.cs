using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// One version of a <see cref="TrackedPackage"/> as the registry lists it.
/// </summary>
[Table(TableNames.TrackedPackageVersions)]
[Index(nameof(TrackedPackageId), nameof(Version), IsUnique = true)]
public class TrackedPackageVersion : AuditableEntity<long>
{
    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The package.
    /// </summary>
    public int TrackedPackageId { get; set; }

    /// <summary>
    /// Version string as the registry spells it.
    /// </summary>
    [MaxLength(TrackedPackage.VersionMaxLength)]
    public string Version { get; set; }

    /// <summary>
    /// UTC instant the version was published; null when the registry does not say (an unlisted NuGet version).
    /// </summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>
    /// Yanked (crates.io, PyPI: every file of the release yanked).
    /// </summary>
    public bool IsYanked { get; set; }

    /// <summary>
    /// Unlisted (NuGet).
    /// </summary>
    public bool IsUnlisted { get; set; }

    /// <summary>
    /// Deprecated (npm, NuGet).
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Deprecation message or yank reason.
    /// </summary>
    [MaxLength(TrackedPackage.MessageMaxLength)]
    public string Message { get; set; }

    /// <summary>
    /// Whether the version has provenance; null when it was not checked (PyPI checks the newest versions only).
    /// </summary>
    public bool? HasProvenance { get; set; }

    /// <summary>
    /// CI run that published the version (from its provenance).
    /// </summary>
    [MaxLength(TrackedPackage.UrlMaxLength)]
    public string CiRunUrl { get; set; }

    /// <summary>
    /// Downloads of the version as the registry counts them (all time, or the last 7 days on npm; see
    /// <see cref="TrackedPackage.VersionDownloadsScope"/>); null where the registry gives none.
    /// </summary>
    public long? Downloads { get; set; }

    /// <summary>
    /// The package.
    /// </summary>
    [ForeignKey(nameof(TrackedPackageId))]
    public virtual TrackedPackage TrackedPackage { get; set; }
}
