namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One tracked keyword's rank before and after a change: the average position over the checked days on which the app was
/// in the results (lower is better).
/// </summary>
public class AsoImpactKeywordDto
{
    /// <summary>
    /// Keyword (normalized).
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Country (ISO code, lower case).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Average rank of the before window; null when not ranked on any checked day.
    /// </summary>
    public double? Before { get; set; }

    /// <summary>
    /// Average rank of the after window; null when not ranked on any checked day.
    /// </summary>
    public double? After { get; set; }

    /// <summary>
    /// <see cref="After"/> minus <see cref="Before"/>: negative is an improvement (closer to the top).
    /// </summary>
    public double? Change { get; set; }

    /// <summary>
    /// Days of the before window the rank was checked.
    /// </summary>
    public int BeforeCheckedDays { get; set; }

    /// <summary>
    /// Days of the before window the app was in the results.
    /// </summary>
    public int BeforeRankedDays { get; set; }

    /// <summary>
    /// Days of the after window the rank was checked.
    /// </summary>
    public int AfterCheckedDays { get; set; }

    /// <summary>
    /// Days of the after window the app was in the results.
    /// </summary>
    public int AfterRankedDays { get; set; }
}
