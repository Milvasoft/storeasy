namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// Before and after report of one change: equal windows before the effect day and after a gap, store page metrics with
/// absolute and relative change, a significance signal for the conversion rate, tracked keyword ranks and the other
/// changes inside the windows.
/// </summary>
public class AsoImpactReportDto
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
    /// Length of each window in days.
    /// </summary>
    public int WindowDays { get; set; }

    /// <summary>
    /// Days skipped from the effect day on.
    /// </summary>
    public int GapDays { get; set; }

    /// <summary>
    /// Whether the windows are whole weeks (every weekday counted equally often).
    /// </summary>
    public bool WholeWeeks { get; set; }

    /// <summary>
    /// Whether the daily averages were taken over only the weekdays both windows have data for (the windows are not
    /// whole weeks, or a window misses days).
    /// </summary>
    public bool WeekdayMatched { get; set; }

    /// <summary>
    /// The window before the change.
    /// </summary>
    public AsoImpactWindowDto Before { get; set; }

    /// <summary>
    /// The window after the gap.
    /// </summary>
    public AsoImpactWindowDto After { get; set; }

    /// <summary>
    /// First day the report is final (the day after the after window).
    /// </summary>
    public DateOnly ReadyDate { get; set; }

    /// <summary>
    /// Country filter, if any.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Traffic source filter, if any.
    /// </summary>
    public TrafficSource? Source { get; set; }

    /// <summary>
    /// Impressions, page views, downloads and conversion rate.
    /// </summary>
    public List<AsoImpactMetricDto> Metrics { get; set; } = [];

    /// <summary>
    /// Significance of the conversion rate change.
    /// </summary>
    public AsoImpactConversionTestDto Conversion { get; set; }

    /// <summary>
    /// Days of the before window (one per day, values null when not reported).
    /// </summary>
    public List<AsoImpactDayDto> BeforeDays { get; set; } = [];

    /// <summary>
    /// Days of the after window.
    /// </summary>
    public List<AsoImpactDayDto> AfterDays { get; set; } = [];

    /// <summary>
    /// Tracked keyword ranks; null when not asked for.
    /// </summary>
    public AsoImpactRanksDto Ranks { get; set; }

    /// <summary>
    /// Other changes of the same store record inside the windows or the gap: the difference may be theirs too.
    /// </summary>
    public List<AsoImpactEventDto> Confounders { get; set; } = [];

    /// <summary>
    /// Changes that went live together with this one (the version and the listing written to it).
    /// </summary>
    public List<AsoImpactEventDto> Related { get; set; } = [];
}
