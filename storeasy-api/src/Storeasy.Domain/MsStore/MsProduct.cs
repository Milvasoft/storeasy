using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.MsStore;

/// <summary>
/// Microsoft Store side of a store record (<see cref="StoreApp"/> with <see cref="StoreKind.Microsoft"/>): which API
/// manages the product and what the last sync learned about it. One row per record.
/// </summary>
[Table(TableNames.MsProducts)]
[Index(nameof(StoreAppId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class MsProduct : AuditableEntity<int>
{
    /// <summary>
    /// Column length of the store ids (submission ids, flight ids, product ids).
    /// </summary>
    public const int IdMaxLength = 50;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Which API manages the product.
    /// </summary>
    public MsProductKind Kind { get; set; }

    /// <summary>
    /// Whether the product is on Pricing Version 2 (the API answers an unknown price tier): its price is managed in
    /// Partner Center.
    /// </summary>
    public bool PricingV2 { get; set; }

    /// <summary>
    /// Whether the product has a published submission. The API works only after the first submission (with the IARC age
    /// rating questionnaire) was made in Partner Center.
    /// </summary>
    public bool FirstSubmissionDone { get; set; }

    /// <summary>
    /// Whether the last published submission makes its packages a mandatory update (the API answers 409 for such apps).
    /// </summary>
    public bool HasMandatoryUpdate { get; set; }

    /// <summary>
    /// MSI/EXE: whether every module of the draft is ready to submit (<c>isReady</c>).
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Last published app submission (MSIX).
    /// </summary>
    [MaxLength(IdMaxLength)]
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>
    /// Pending app submission (MSIX) or ongoing submission (MSI/EXE). Microsoft allows one at a time.
    /// </summary>
    [MaxLength(IdMaxLength)]
    public string PendingSubmissionId { get; set; }

    /// <summary>
    /// UTC instant of the last successful sync. Null until the store confirmed the product.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
