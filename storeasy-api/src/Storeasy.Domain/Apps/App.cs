using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Apps;

/// <summary>
/// A storeasy app: one product that lives in one, two or all three stores. Its store records (<see cref="StoreApps"/>,
/// at most one per store) carry the store specific identifiers; nothing store specific is kept here.
/// </summary>
[Table(TableNames.Apps)]
[Index(nameof(OrganizationId))]
public class App : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Display name (the store name of the first record by default).
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Icon address, when known.
    /// </summary>
    [MaxLength(1000)]
    public string IconUrl { get; set; }

    /// <summary>
    /// What the rules catalog needs to know about the app that the stores do not report (device families, regions,
    /// features). Null until a member fills it.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public AppProfile Profile { get; set; }

    /// <summary>
    /// UTC instant a member hid the app from the apps list; null while it is shown. Only a member changes it (sync never
    /// writes it), so a hidden app stays hidden when its records are read from the store again.
    /// </summary>
    public DateTime? HiddenDate { get; set; }

    /// <summary>
    /// Store records of the app.
    /// </summary>
    public virtual List<StoreApp> StoreApps { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
