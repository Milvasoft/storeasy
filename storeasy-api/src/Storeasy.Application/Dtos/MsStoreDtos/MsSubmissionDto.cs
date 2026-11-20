using Milvasoft.Attributes.Annotations;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.MsStore;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A Microsoft Store submission with its status history. <see cref="ModifiedOutsideApi"/> is the Partner Center rule: a
/// submission storeasy created and someone changed in Partner Center cannot be changed or committed by the API anymore.
/// </summary>
public class MsSubmissionDto
{
    /// <summary>
    /// Row id.
    /// </summary>
    public long Id { get; set; }

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
    public string SubmissionId { get; set; }

    /// <summary>
    /// Package flight of a flight submission.
    /// </summary>
    public string FlightId { get; set; }

    /// <summary>
    /// Add-on of an add-on submission.
    /// </summary>
    public string AddonStoreId { get; set; }

    /// <summary>
    /// Name Partner Center shows.
    /// </summary>
    public string FriendlyName { get; set; }

    /// <summary>
    /// Highest package version.
    /// </summary>
    public string PackageVersion { get; set; }

    /// <summary>
    /// Current status.
    /// </summary>
    public MsSubmissionStatus Status { get; set; }

    /// <summary>
    /// Statuses seen, in order.
    /// </summary>
    public List<MsStatusChange> StatusHistory { get; set; }

    /// <summary>
    /// Errors, warnings and certification reports.
    /// </summary>
    public MsStatusDetails StatusDetails { get; set; }

    /// <summary>
    /// Whether the submission is pending.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Whether storeasy created it.
    /// </summary>
    public bool CreatedByStoreasy { get; set; }

    /// <summary>
    /// Whether it was changed in Partner Center after storeasy created it.
    /// </summary>
    public bool ModifiedOutsideApi { get; set; }

    /// <summary>
    /// Gradual rollout percentage.
    /// </summary>
    public double? RolloutPercentage { get; set; }

    /// <summary>
    /// Gradual rollout state.
    /// </summary>
    public MsRolloutStatus? RolloutStatus { get; set; }

    /// <summary>
    /// When the submission publishes after certification; null when the store did not report it.
    /// </summary>
    public MsPublishMode? PublishMode { get; set; }

    /// <summary>
    /// UTC publish date of a <see cref="MsPublishMode.SpecificDate"/> submission.
    /// </summary>
    public DateTime? PublishDate { get; set; }

    /// <summary>
    /// Whether the submission passed certification and waits for "Publish now" in Partner Center
    /// (<see cref="MsSubmissionJson.AwaitsManualPublish"/>).
    /// </summary>
    public bool AwaitsManualPublish => MsSubmissionJson.AwaitsManualPublish(IsPending, Status, PublishMode);

    /// <summary>
    /// UTC instant of the last read.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Projection from a submission row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MsSubmission, MsSubmissionDto>> Projection { get; } = s => new MsSubmissionDto
    {
        Id = s.Id,
        StoreAppId = s.StoreAppId,
        Target = s.Target,
        SubmissionId = s.ExternalId,
        FlightId = s.FlightId,
        AddonStoreId = s.AddonStoreId,
        FriendlyName = s.FriendlyName,
        PackageVersion = s.PackageVersion,
        Status = s.Status,
        StatusHistory = s.StatusHistory,
        StatusDetails = s.StatusDetails,
        IsPending = s.IsPending,
        CreatedByStoreasy = s.CreatedByStoreasy,
        ModifiedOutsideApi = s.ModifiedOutsideApi,
        RolloutPercentage = s.RolloutPercentage,
        RolloutStatus = s.RolloutStatus,
        PublishMode = s.PublishMode,
        PublishDate = s.PublishDate,
        SyncedDate = s.SyncedDate,
    };
}
