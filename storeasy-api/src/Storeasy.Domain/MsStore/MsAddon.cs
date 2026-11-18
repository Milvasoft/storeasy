using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.MsStore;

/// <summary>
/// An add-on (in-app product) of an MSIX app. Store-managed consumables and products with mandatory updates cannot be
/// written by the API (it answers 409): <see cref="ApiWritable"/> is false and <see cref="AssistedReason"/> says why.
/// </summary>
[Table(TableNames.MsAddons)]
[Index(nameof(StoreAppId), nameof(StoreId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class MsAddon : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="ProductId"/>.
    /// </summary>
    public const int ProductIdMaxLength = 100;

    /// <summary>
    /// Column length of <see cref="Title"/>.
    /// </summary>
    public const int TitleMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record of the app the add-on belongs to.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The store's id of the add-on (9NBLGGH4TNMP).
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string StoreId { get; set; }

    /// <summary>
    /// Developer's product id.
    /// </summary>
    [MaxLength(ProductIdMaxLength)]
    public string ProductId { get; set; }

    /// <summary>
    /// Title in the first listing, when known.
    /// </summary>
    [MaxLength(TitleMaxLength)]
    public string Title { get; set; }

    /// <summary>
    /// Product type.
    /// </summary>
    public MsAddonType Type { get; set; }

    /// <summary>
    /// Lifetime of a durable add-on (Forever, OneMonth...), when known.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string Lifetime { get; set; }

    /// <summary>
    /// Price tier of the last read submission (Free, Tier2...). Null when Partner Center manages the price.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string PriceId { get; set; }

    /// <summary>
    /// Last published add-on submission.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>
    /// Pending add-on submission.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string PendingSubmissionId { get; set; }

    /// <summary>
    /// Whether the API may write the add-on.
    /// </summary>
    public bool ApiWritable { get; set; } = true;

    /// <summary>
    /// Why the add-on is changed in Partner Center, when <see cref="ApiWritable"/> is false.
    /// </summary>
    public MsAssistedReason? AssistedReason { get; set; }

    /// <summary>
    /// UTC instant of the last read from the store.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
