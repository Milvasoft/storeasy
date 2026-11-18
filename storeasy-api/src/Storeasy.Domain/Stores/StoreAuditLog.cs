using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Stores;

/// <summary>
/// One write in an organization: to a store (listing, screenshot, release...) or to the organization itself (member,
/// connection). Rows are never updated; the organization's members with <see cref="OrgPermission.View"/> read them.
/// </summary>
[Table(TableNames.StoreAuditLogs)]
[Index(nameof(OrganizationId), nameof(Date), IsDescending = [false, true])]
public class StoreAuditLog : BaseEntity<long>
{
    /// <summary>
    /// Organization the write happened in.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Acting user. Null for system actions (webhook, scheduled sync).
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// User name of the actor at the time of the action ("system" for system actions).
    /// </summary>
    [MaxLength(100)]
    public string UserName { get; set; }

    /// <summary>
    /// Where the action came from.
    /// </summary>
    public ActionChannel Channel { get; set; }

    /// <summary>
    /// What happened.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// Store the action touched. Null for organization actions.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Id of the affected record (connection id, member id, store app id...).
    /// </summary>
    [MaxLength(100)]
    public string TargetId { get; set; }

    /// <summary>
    /// One line, human readable summary without secrets.
    /// </summary>
    [MaxLength(500)]
    public string Summary { get; set; }

    /// <summary>
    /// Optional structured details (field level changes), JSON.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Details { get; set; }

    /// <summary>
    /// UTC instant of the action.
    /// </summary>
    public DateTime Date { get; set; }
}
