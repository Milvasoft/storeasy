using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// Where a package of one platform of an app goes after it passed the checks (design 13.18). One rule per app and
/// platform. Targets are test channels only (<see cref="IntakeRules.IsTargetAllowed"/>): production and review
/// submission are never automatic.
/// </summary>
[Table(TableNames.DeliveryRules)]
[Index(nameof(AppId), nameof(Platform), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class DeliveryRule : CreationAuditableEntity<int>
{
    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Platform the rule is for.
    /// </summary>
    public ArtifactPlatform Platform { get; set; }

    /// <summary>
    /// Whether packages are delivered automatically.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Target kind.
    /// </summary>
    public DeliveryTarget Target { get; set; }

    /// <summary>
    /// Target id: TestFlight group id, Play track name, flight id.
    /// </summary>
    [MaxLength(200)]
    public string TargetId { get; set; }

    /// <summary>
    /// Target display name.
    /// </summary>
    [MaxLength(200)]
    public string TargetName { get; set; }

    /// <summary>
    /// What to do when the checks pass with warnings.
    /// </summary>
    public DeliveryWarningAction OnWarning { get; set; } = DeliveryWarningAction.Hold;

    /// <summary>
    /// Staged rollout fraction (0-1) of a Play testing track release; null releases to every tester.
    /// </summary>
    public double? RolloutFraction { get; set; }

    /// <summary>
    /// Member who changed the rule last.
    /// </summary>
    public int? UpdatedByUserId { get; set; }

    /// <summary>
    /// Name of that member.
    /// </summary>
    [MaxLength(256)]
    public string UpdatedByUserName { get; set; }

    /// <summary>
    /// UTC instant of the last change.
    /// </summary>
    public DateTime UpdatedDate { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    public virtual App App { get; set; }
}
