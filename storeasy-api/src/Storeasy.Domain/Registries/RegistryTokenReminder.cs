using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// A reminder about a long lived registry token a member uses somewhere (a CI secret): its label, registry, where it is
/// used and its expiry date. The token itself is never stored or accepted. storeasy warns 14 and 3 days before and on the
/// expiry date and suggests trusted publishing instead.
/// </summary>
[Table(TableNames.RegistryTokenReminders)]
[Index(nameof(OrganizationId))]
[Index(nameof(ExpiryDate))]
public class RegistryTokenReminder : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of <see cref="Label"/>.
    /// </summary>
    public const int LabelMaxLength = 100;

    /// <summary>
    /// Maximum length of <see cref="UsedIn"/>.
    /// </summary>
    public const int UsedInMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Registry the token is for.
    /// </summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>
    /// A name for the token (for example "npm publish token"); never the token.
    /// </summary>
    [MaxLength(LabelMaxLength)]
    public string Label { get; set; }

    /// <summary>
    /// Where the token is used, for example the CI secret name and repository; never the token.
    /// </summary>
    [MaxLength(UsedInMaxLength)]
    public string UsedIn { get; set; }

    /// <summary>
    /// The day the token expires (UTC).
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// A tracked package the token publishes, when a member linked one.
    /// </summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>
    /// The last warning sent: 0 none, 1 the 14 day, 2 the 3 day, 3 the expiry warning. Reset when the expiry date changes.
    /// </summary>
    public byte WarnedStage { get; set; }

    /// <summary>
    /// The linked package.
    /// </summary>
    [ForeignKey(nameof(TrackedPackageId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual TrackedPackage TrackedPackage { get; set; }
}
