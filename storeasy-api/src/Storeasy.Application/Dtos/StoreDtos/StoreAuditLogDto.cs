using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Stores;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.StoreDtos;

/// <summary>
/// One row of the organization audit log.
/// </summary>
public class StoreAuditLogDto
{
    /// <summary>
    /// Entry id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// UTC instant.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Actor user name ("system" for system actions).
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Channel.
    /// </summary>
    public ActionChannel Channel { get; set; }

    /// <summary>
    /// Action.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// Store, when the action touched one.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Affected record id.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Summary line.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Structured details (JSON) or null.
    /// </summary>
    public string Details { get; set; }

    /// <summary>
    /// Projection from an audit row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<StoreAuditLog, StoreAuditLogDto>> Projection { get; } = a => new StoreAuditLogDto
    {
        Id = a.Id,
        Date = a.Date,
        UserName = a.UserName,
        Channel = a.Channel,
        Action = a.Action,
        Store = a.Store,
        TargetId = a.TargetId,
        Summary = a.Summary,
        Details = a.Details,
    };
}
