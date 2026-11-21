using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// The date range of a metrics read and its previous period of the same length.
/// </summary>
public sealed record StoreMetricRange
{
    /// <summary>
    /// A range.
    /// </summary>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day.</param>
    public StoreMetricRange(DateOnly from, DateOnly to)
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// First day.
    /// </summary>
    public DateOnly From { get; init; }

    /// <summary>
    /// Last day.
    /// </summary>
    public DateOnly To { get; init; }

    /// <summary>
    /// Range length used when a request names neither <c>From</c> nor <c>Days</c>.
    /// </summary>
    public const int DefaultDays = 30;

    /// <summary>
    /// Days in the range (both ends included).
    /// </summary>
    public int Days => To.DayNumber - From.DayNumber + 1;

    /// <summary>
    /// First day of the previous period.
    /// </summary>
    public DateOnly PreviousFrom => From.AddDays(-Days);

    /// <summary>
    /// Last day of the previous period.
    /// </summary>
    public DateOnly PreviousTo => From.AddDays(-1);

    /// <summary>
    /// Resolves a request's range: <paramref name="to"/> defaults to yesterday (UTC; the stores report a day late at the
    /// earliest), <paramref name="from"/> to <paramref name="days"/> days ending at <paramref name="to"/>. Null when the
    /// range is reversed or longer than <see cref="StoreMetricRules.MaxRangeDays"/>.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="days"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static StoreMetricRange Resolve(DateOnly? from, DateOnly? to, int? days, DateTimeOffset now)
    {
        var end = to ?? DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var start = from ?? end.AddDays(-((days is > 0 ? days.Value : DefaultDays) - 1));
        var range = new StoreMetricRange(start, end);

        return start > end || range.Days > StoreMetricRules.MaxRangeDays ? null : range;
    }
}
