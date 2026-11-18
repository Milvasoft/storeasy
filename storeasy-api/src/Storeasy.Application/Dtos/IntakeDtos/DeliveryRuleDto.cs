using Storeasy.Domain.Intake;
using System.Linq.Expressions;

namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// The delivery rule of one platform.
/// </summary>
public class DeliveryRuleDto
{
    /// <summary>
    /// Platform.
    /// </summary>
    public ArtifactPlatform Platform { get; set; }

    /// <summary>
    /// Whether packages are delivered automatically.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Target kind.
    /// </summary>
    public DeliveryTarget Target { get; set; }

    /// <summary>
    /// TestFlight group id, Play track or flight id.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// What to do on a warning.
    /// </summary>
    public DeliveryWarningAction OnWarning { get; set; }

    /// <summary>
    /// Staged rollout fraction of a Play release.
    /// </summary>
    public double? RolloutFraction { get; set; }

    /// <summary>
    /// The columns a rule DTO needs, read from the entity.
    /// </summary>
    public static Expression<Func<DeliveryRule, DeliveryRule>> EntityProjection { get; } = r => new DeliveryRule
    {
        AppId = r.AppId,
        Platform = r.Platform,
        Enabled = r.Enabled,
        Target = r.Target,
        TargetId = r.TargetId,
        TargetName = r.TargetName,
        OnWarning = r.OnWarning,
        RolloutFraction = r.RolloutFraction,
        UpdatedByUserName = r.UpdatedByUserName,
        UpdatedDate = r.UpdatedDate,
    };

    /// <summary>
    /// A DTO of a rule read with <see cref="EntityProjection"/>.
    /// </summary>
    /// <param name="rule"></param>
    /// <returns></returns>
    public static DeliveryRuleDto From(DeliveryRule rule) => new()
    {
        Platform = rule.Platform,
        Enabled = rule.Enabled,
        Target = rule.Target,
        TargetId = rule.TargetId,
        TargetName = rule.TargetName,
        OnWarning = rule.OnWarning,
        RolloutFraction = rule.RolloutFraction,
    };
}
