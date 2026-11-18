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
    /// Members of the organization.
    /// </summary>
    public virtual List<OrganizationMember> Members { get; set; }
}
