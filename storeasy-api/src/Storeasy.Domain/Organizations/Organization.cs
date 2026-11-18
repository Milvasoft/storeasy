using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// A team that owns store connections and apps. Every store record, job and audit row belongs to exactly one
/// organization, and a user reaches them only through an <see cref="OrganizationMember"/> row.
/// </summary>
[Table(TableNames.Organizations)]
[Index(nameof(Slug), IsUnique = true)]
public class Organization : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>; the validators use the same limit.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Column length of <see cref="Slug"/>; the validators use the same limit.
    /// </summary>
    public const int SlugMaxLength = 60;

    /// <summary>
    /// Default of <see cref="AuditRetentionDays"/>.
    /// </summary>
    public const int DefaultAuditRetentionDays = 365;

    /// <summary>
    /// The audit log retention choices (design 15.20).
    /// </summary>
    public static readonly int[] AuditRetentionChoices = [90, 180, 365, 730];

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// URL friendly unique key (lower case letters, digits and hyphens).
    /// </summary>
    [MaxLength(SlugMaxLength)]
    public string Slug { get; set; }

    /// <summary>
    /// How many days audit log entries are kept; older ones are deleted every night.
    /// </summary>
    public int AuditRetentionDays { get; set; } = DefaultAuditRetentionDays;

    /// <summary>
    /// Default language of the organization's store texts (e.g. tr-TR); null when not chosen.
    /// </summary>
    [MaxLength(10)]
    public string DefaultLocale { get; set; }

    /// <summary>
    /// Members of the organization.
    /// </summary>
    public virtual List<OrganizationMember> Members { get; set; }
}
