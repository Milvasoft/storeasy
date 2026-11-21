namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// The headline of one change's before and after report.
/// </summary>
public class AsoImpactSummaryDto
{
    /// <summary>
    /// The change.
    /// </summary>
    public AsoImpactEventDto Event { get; set; }

    /// <summary>
    /// Whether the report can be read yet.
    /// </summary>
    public ImpactStatus Status { get; set; }

    /// <summary>
    /// Page views per day before and after.
    /// </summary>
    public AsoImpactMetricDto PageViews { get; set; }

    /// <summary>
    /// Downloads per day before and after.
    /// </summary>
    public AsoImpactMetricDto Downloads { get; set; }

    /// <summary>
    /// Conversion rate before and after.
    /// </summary>
    public AsoImpactMetricDto ConversionRate { get; set; }

    /// <summary>
    /// Significance of the conversion rate change.
    /// </summary>
    public ImpactSignal Signal { get; set; }

    /// <summary>
    /// Whether another change falls inside the windows.
    /// </summary>
    public bool Confounded { get; set; }
}
