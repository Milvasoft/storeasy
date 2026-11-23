using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// One day of a tracked package's downloads: the registry's daily count where it gives one (npm, PyPI, crates.io), and
/// storeasy's snapshot of the all time total on that day (every registry with a total), so a trend exists even where the
/// registry gives totals only.
/// </summary>
[Table(TableNames.PackageDownloadDays)]
[Index(nameof(TrackedPackageId), nameof(Day), IsUnique = true)]
public class PackageDownloadDay : CreationAuditableEntity<long>
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
    /// The UTC day.
    /// </summary>
    public DateTime Day { get; set; }

    /// <summary>
    /// Downloads on that day as the registry counts them; null where it gives no daily counts.
    /// </summary>
    public long? Downloads { get; set; }

    /// <summary>
    /// All time downloads storeasy read on that day; null where the registry gives no total (PyPI).
    /// </summary>
    public long? TotalDownloads { get; set; }

    /// <summary>
    /// The package.
    /// </summary>
    [ForeignKey(nameof(TrackedPackageId))]
    public virtual TrackedPackage TrackedPackage { get; set; }
}
