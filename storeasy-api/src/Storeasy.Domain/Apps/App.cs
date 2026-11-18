using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
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
    /// Store records of the app.
    /// </summary>
    public virtual List<StoreApp> StoreApps { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
