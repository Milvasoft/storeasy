using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Monetization;

/// <summary>
/// An App Store subscription group of a store record, as last synced from the store. A customer can hold one
/// subscription of a group at a time; level 1 is the highest service level. Play has no groups.
/// </summary>
[Table(TableNames.ProductGroups)]
[Index(nameof(StoreAppId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class ProductGroup : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="ReferenceName"/>.
    /// </summary>
    public const int ReferenceNameMaxLength = 64;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the group.
    /// </summary>
    [MaxLength(StoreApp.ExternalIdMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// Reference name (only visible in App Store Connect).
    /// </summary>
    [MaxLength(ReferenceNameMaxLength)]
    public string ReferenceName { get; set; }

    /// <summary>
    /// Localizations (display name, custom app name).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<ProductLocalization> Localizations { get; set; } = [];

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
