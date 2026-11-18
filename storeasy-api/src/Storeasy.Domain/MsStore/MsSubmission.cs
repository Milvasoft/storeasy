using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.MsStore;

/// <summary>
/// A Microsoft Store submission storeasy knows: of an app, a package flight, an add-on or an MSI/EXE product. Rows are
/// written by the sync and by storeasy's own writes. <see cref="ContentHash"/> is the fingerprint of the content storeasy
/// last wrote; when a later read differs, the submission was changed in Partner Center and the API cannot change or
/// commit it anymore (<see cref="ModifiedOutsideApi"/>).
/// </summary>
[Table(TableNames.MsSubmissions)]
[Index(nameof(StoreAppId), nameof(Target), nameof(ExternalId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class MsSubmission : AuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="FriendlyName"/>.
    /// </summary>
    public const int FriendlyNameMaxLength = 255;

    /// <summary>
    /// Column length of <see cref="PackageVersion"/>.
    /// </summary>
    public const int PackageVersionMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// What the submission belongs to.
    /// </summary>
    public MsSubmissionTarget Target { get; set; }

    /// <summary>
    /// The store's submission id.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// Package flight of a flight submission.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string FlightId { get; set; }

    /// <summary>
    /// Store id of the add-on of an add-on submission.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string AddonStoreId { get; set; }

    /// <summary>
    /// Name Partner Center shows.
    /// </summary>
    [MaxLength(FriendlyNameMaxLength)]
    public string FriendlyName { get; set; }

    /// <summary>
    /// Current status.
    /// </summary>
    public MsSubmissionStatus Status { get; set; }

    /// <summary>
    /// Every status storeasy saw, in order.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<MsStatusChange> StatusHistory { get; set; } = [];

    /// <summary>
    /// Errors, warnings and certification reports of the last read.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public MsStatusDetails StatusDetails { get; set; }

    /// <summary>
    /// Whether this is the pending submission of its target (not published yet).
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Whether storeasy created the submission through the API.
    /// </summary>
    public bool CreatedByStoreasy { get; set; }

    /// <summary>
    /// Whether a submission storeasy created was changed in Partner Center afterwards.
    /// </summary>
    public bool ModifiedOutsideApi { get; set; }

    /// <summary>
    /// Fingerprint (SHA-256, hex) of the content storeasy last wrote. Null when storeasy never wrote it.
    /// </summary>
    [MaxLength(64)]
    public string ContentHash { get; set; }

    /// <summary>
    /// Highest package version in the submission, when it has packages.
    /// </summary>
    [MaxLength(PackageVersionMaxLength)]
    public string PackageVersion { get; set; }

    /// <summary>
    /// Gradual rollout percentage, when the submission rolls out gradually.
    /// </summary>
    public double? RolloutPercentage { get; set; }

    /// <summary>
    /// Gradual rollout state, when the submission rolls out gradually.
    /// </summary>
    public MsRolloutStatus? RolloutStatus { get; set; }

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
