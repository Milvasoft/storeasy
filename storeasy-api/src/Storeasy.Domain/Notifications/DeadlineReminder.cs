using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Notifications;

/// <summary>
/// A deadline reminder that was sent (TestFlight build expiry, compliance deadline), so each threshold is notified once.
/// </summary>
[Table(TableNames.DeadlineReminders)]
[Index(nameof(Subject), nameof(ThresholdDays), IsUnique = true)]
public class DeadlineReminder : BaseEntity<long>
{
    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// What the deadline is about (<c>build:{id}</c>, <c>rule:{ruleId}:app:{appId}:{dueDate}</c>).
    /// </summary>
    [MaxLength(300)]
    public string Subject { get; set; }

    /// <summary>
    /// The threshold (days before the deadline) that fired.
    /// </summary>
    public int ThresholdDays { get; set; }

    /// <summary>
    /// UTC instant the reminder was sent.
    /// </summary>
    public DateTime SentDate { get; set; }
}
