namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// Two proportion z-test of the conversion rate (downloads over page views) of the two windows.
/// </summary>
public class AsoImpactConversionTestDto
{
    /// <summary>
    /// How sure the change is.
    /// </summary>
    public ImpactSignal Signal { get; set; }

    /// <summary>
    /// z score (positive: the rate went up); null without a test.
    /// </summary>
    public double? ZScore { get; set; }

    /// <summary>
    /// Two sided p-value; null without a test.
    /// </summary>
    public double? PValue { get; set; }

    /// <summary>
    /// Downloads of the before window (of the days that report page views).
    /// </summary>
    public long BeforeDownloads { get; set; }

    /// <summary>
    /// Page views of the before window.
    /// </summary>
    public long BeforePageViews { get; set; }

    /// <summary>
    /// Downloads of the after window.
    /// </summary>
    public long AfterDownloads { get; set; }

    /// <summary>
    /// Page views of the after window.
    /// </summary>
    public long AfterPageViews { get; set; }
}
