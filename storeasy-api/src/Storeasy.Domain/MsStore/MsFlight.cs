using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.MsStore;

/// <summary>
/// A package flight of an MSIX app: packages delivered to the customers in its flight groups. A customer in several
/// flights gets the packages of the highest ranked one. Groups are defined in Partner Center; the API only names their ids.
/// </summary>
[Table(TableNames.MsFlights)]
[Index(nameof(StoreAppId), nameof(FlightId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class MsFlight : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="FriendlyName"/>.
    /// </summary>
    public const int FriendlyNameMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The store's flight id.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string FlightId { get; set; }

    /// <summary>
    /// Flight name.
    /// </summary>
    [MaxLength(FriendlyNameMaxLength)]
    public string FriendlyName { get; set; }

    /// <summary>
    /// Rank (1 is the highest), when the store reports it.
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// Flight group ids.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> GroupIds { get; set; } = [];

    /// <summary>
    /// Last published flight submission.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>
    /// Pending flight submission.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string PendingSubmissionId { get; set; }

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
