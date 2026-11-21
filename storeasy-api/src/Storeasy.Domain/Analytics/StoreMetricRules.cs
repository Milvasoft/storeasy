namespace Storeasy.Domain.Analytics;

/// <summary>
/// Shared rules of the store page metrics.
/// </summary>
public static class StoreMetricRules
{
    /// <summary>
    /// <see cref="StoreMetric.Country"/> of a row that is not split by country (the total of every country).
    /// </summary>
    public const string AllCountries = "";

    /// <summary>
    /// Country code used when the store does not name the country of an event.
    /// </summary>
    public const string UnknownCountry = "ZZ";

    /// <summary>
    /// Column length of <see cref="StoreMetric.Country"/>.
    /// </summary>
    public const int CountryMaxLength = 8;

    /// <summary>
    /// Longest date range one query reads, in days.
    /// </summary>
    public const int MaxRangeDays = 400;

    /// <summary>
    /// Months of daily rows kept; <c>storeMetric_cleanup.sql</c> deletes older ones.
    /// </summary>
    public const int RetentionMonths = 25;

    /// <summary>
    /// Months of Google Play report files read the first time a record is synced (the current month included).
    /// </summary>
    public const int PlayBackfillMonths = 6;

    /// <summary>
    /// Days the Microsoft Store analytics API is read back the first time a record is synced.
    /// </summary>
    public const int MicrosoftBackfillDays = 90;

    /// <summary>
    /// Days read again on every later sync, because the stores complete and correct recent days late.
    /// </summary>
    public const int RefreshDays = 7;

    /// <summary>
    /// Normalizes a store's country or territory code: upper case, trimmed, <see cref="UnknownCountry"/> when empty or too long.
    /// </summary>
    /// <param name="country"></param>
    /// <returns></returns>
    public static string NormalizeCountry(string country)
    {
        var value = country?.Trim().ToUpperInvariant();

        return string.IsNullOrEmpty(value) || value.Length > CountryMaxLength ? UnknownCountry : value;
    }

    /// <summary>
    /// Normalizes the country of a stored row: empty (or null) stays <see cref="AllCountries"/>, anything else goes
    /// through <see cref="NormalizeCountry"/>.
    /// </summary>
    /// <param name="country"></param>
    /// <returns></returns>
    public static string NormalizeSlice(string country) => string.IsNullOrEmpty(country) ? AllCountries : NormalizeCountry(country);

    /// <summary>
    /// Downloads divided by page views, or null when there were no page views.
    /// </summary>
    /// <param name="downloads">Downloads counted where page views are known.</param>
    /// <param name="pageViews"></param>
    /// <returns></returns>
    public static double? ConversionRate(long? downloads, long? pageViews)
        => pageViews is > 0 && downloads.HasValue ? Math.Round((double)downloads.Value / pageViews.Value, 6) : null;

    /// <summary>
    /// Change from <paramref name="previous"/> to <paramref name="current"/> as a fraction (0.25 is 25 percent more), or
    /// null when there is nothing to compare with.
    /// </summary>
    /// <param name="current"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public static double? Change(double? current, double? previous)
        => current.HasValue && previous is > 0 ? Math.Round((current.Value - previous.Value) / previous.Value, 6) : null;
}
