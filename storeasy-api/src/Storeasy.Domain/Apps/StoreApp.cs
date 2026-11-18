using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Apps;

/// <summary>
/// An app's record in one store. <see cref="ExternalId"/> is the store's own id of the app: the App Store Connect app id,
/// the Play package name, the Microsoft Store product id. <see cref="BundleId"/> is the platform identifier (iOS bundle id,
/// Android package name, Windows package family name) that links the records of one product across stores.
/// </summary>
[Table(TableNames.StoreApps)]
[Index(nameof(OrganizationId), nameof(Store), nameof(ExternalId), IsUnique = true)]
[Index(nameof(AppId), nameof(Store), IsUnique = true)]
public class StoreApp : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="ExternalId"/>.
    /// </summary>
    public const int ExternalIdMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app the record belongs to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store connection the record is read with. Null when its connection was removed.
    /// </summary>
    public int? StoreConnectionId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the app.
    /// </summary>
    [MaxLength(ExternalIdMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// Platform identifier (bundle id, package name, package family name).
    /// </summary>
    [MaxLength(255)]
    public string BundleId { get; set; }

    /// <summary>
    /// Name in the store.
    /// </summary>
    [MaxLength(App.NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// UTC instant of the last successful sync.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    [ForeignKey(nameof(AppId))]
    public virtual App App { get; set; }

    /// <summary>
    /// Navigation property of the connection.
    /// </summary>
    [ForeignKey(nameof(StoreConnectionId))]
    public virtual StoreConnection StoreConnection { get; set; }

    /// <summary>
    /// Builds of the record.
    /// </summary>
    public virtual List<Build> Builds { get; set; }
}
