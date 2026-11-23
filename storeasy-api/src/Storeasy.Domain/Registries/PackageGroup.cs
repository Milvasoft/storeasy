using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// A named group of tracked packages that are released together (for example every Milvasoft.* package of one
/// repository). A package belongs to at most one group.
/// </summary>
[Table(TableNames.PackageGroups)]
[Index(nameof(OrganizationId), nameof(Name), IsUnique = true)]
public class PackageGroup : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name of the group.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// How a release of the group starts in its repository's CI; null until set.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ReleaseSettings Release { get; set; }

    /// <summary>
    /// Member packages.
    /// </summary>
    public virtual List<TrackedPackage> Packages { get; set; }
}
