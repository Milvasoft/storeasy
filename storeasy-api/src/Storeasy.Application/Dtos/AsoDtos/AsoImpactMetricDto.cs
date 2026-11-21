namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One metric before and after a change. Counts (impressions, page views, downloads) are compared as daily averages over
/// the same weekdays, so a window with a missing day is not penalized; the conversion rate is total downloads over total
/// page views of each window.
/// </summary>
public class AsoImpactMetricDto
{
    /// <summary>
    /// Metric.
    /// </summary>
    public StoreMetricKind Metric { get; set; }

    /// <summary>
    /// Daily average (or the rate) in the before window; null when not reported.
    /// </summary>
    public double? Before { get; set; }

    /// <summary>
    /// Daily average (or the rate) in the after window; null when not reported.
    /// </summary>
    public double? After { get; set; }

    /// <summary>
    /// Total of the before window (counts only).
    /// </summary>
    public long? BeforeTotal { get; set; }

    /// <summary>
    /// Total of the after window (counts only).
    /// </summary>
    public long? AfterTotal { get; set; }

    /// <summary>
    /// <see cref="After"/> minus <see cref="Before"/> (per day for counts, rate points for the conversion rate).
    /// </summary>
    public double? AbsoluteChange { get; set; }

    /// <summary>
    /// Relative change (0.1 is 10 percent up); null when the before value is 0 or missing.
    /// </summary>
    public double? RelativeChange { get; set; }
}
