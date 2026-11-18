using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.MsStore;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A package flight with the names of its groups (recorded in storeasy, since the API only names their ids).
/// </summary>
public class MsFlightDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Flight id.
    /// </summary>
    public string FlightId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string FriendlyName { get; set; }

    /// <summary>
    /// Rank (1 is the highest), when known.
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// Flight group ids.
    /// </summary>
    public List<string> GroupIds { get; set; }

    /// <summary>
    /// Flight groups with their recorded names.
    /// </summary>
    public List<MsFlightGroupDto> Groups { get; set; } = [];

    /// <summary>
    /// Last published flight submission.
    /// </summary>
    public string LastPublishedSubmissionId { get; set; }

    /// <summary>
    /// Pending flight submission.
    /// </summary>
    public string PendingSubmissionId { get; set; }

    /// <summary>
    /// UTC instant of the last read.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Projection from a flight row (groups are filled afterwards in one query).
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MsFlight, MsFlightDto>> Projection { get; } = f => new MsFlightDto
    {
        StoreAppId = f.StoreAppId,
        FlightId = f.FlightId,
        FriendlyName = f.FriendlyName,
        Rank = f.Rank,
        GroupIds = f.GroupIds,
        LastPublishedSubmissionId = f.LastPublishedSubmissionId,
        PendingSubmissionId = f.PendingSubmissionId,
        SyncedDate = f.SyncedDate,
    };
}
