using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// An alert of a <see cref="TrackedPackage"/>: open while its condition holds, resolved (with the date) by the sync that sees
/// the condition gone.
/// </summary>
[Table(TableNames.PackageAlerts)]
[Index(nameof(TrackedPackageId), nameof(ResolvedDate))]
[Index(nameof(OrganizationId), nameof(ResolvedDate))]
public class PackageAlert : AuditableEntity<long>
{
    /// <summary>
    /// Maximum length of <see cref="AdvisoryId"/>.
    /// </summary>
    public const int AdvisoryIdMaxLength = 100;

    /// <summary>
    /// Maximum length of <see cref="Severity"/>.
    /// </summary>
    public const int SeverityMaxLength = 20;

    /// <summary>
    /// Maximum length of <see cref="Summary"/>.
    /// </summary>
    public const int SummaryMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The package.
    /// </summary>
    public int TrackedPackageId { get; set; }

    /// <summary>
    /// What the alert is about.
    /// </summary>
    public PackageAlertKind Kind { get; set; }

    /// <summary>
    /// The version the alert is about.
    /// </summary>
    [MaxLength(TrackedPackage.VersionMaxLength)]
    public string Version { get; set; }

    /// <summary>
    /// Advisory id (<c>GHSA-...</c>, <c>RUSTSEC-...</c>, <c>PYSEC-...</c>) of an <see cref="PackageAlertKind.Advisory"/> alert.
    /// </summary>
    [MaxLength(AdvisoryIdMaxLength)]
    public string AdvisoryId { get; set; }

    /// <summary>
    /// Severity of an advisory (<c>LOW</c>, <c>MODERATE</c>, <c>HIGH</c>, <c>CRITICAL</c>) when the advisory database gives one.
    /// </summary>
    [MaxLength(SeverityMaxLength)]
    public string Severity { get; set; }

    /// <summary>
    /// One line: the advisory summary, the deprecation message or the yank reason.
    /// </summary>
    [MaxLength(SummaryMaxLength)]
    public string Summary { get; set; }

    /// <summary>
    /// Address with the details (the advisory).
    /// </summary>
    [MaxLength(TrackedPackage.UrlMaxLength)]
    public string Url { get; set; }

    /// <summary>
    /// UTC instant the alert was opened.
    /// </summary>
    public DateTime OpenedDate { get; set; }

    /// <summary>
    /// UTC instant the alert was resolved; null while open.
    /// </summary>
    public DateTime? ResolvedDate { get; set; }

    /// <summary>
    /// The package.
    /// </summary>
    [ForeignKey(nameof(TrackedPackageId))]
    public virtual TrackedPackage TrackedPackage { get; set; }
}
