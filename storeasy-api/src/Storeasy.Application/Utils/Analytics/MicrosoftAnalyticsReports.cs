using System.Globalization;
using System.Text.Json;

namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// The Microsoft Store analytics API answers storeasy reads (<c>manage.devcenter.microsoft.com/v1.0/my/analytics</c>):
/// <list type="bullet">
/// <item><c>appacquisitions</c> with <c>aggregationLevel=day</c> and <c>groupby=date,market,storeClient</c>:
/// <c>Value[]</c> of <c>date</c>, <c>market</c>, <c>storeClient</c>, <c>acquisitionQuantity</c>; downloads by country
/// and store client.</item>
/// <item><c>funnel</c> for one day (<c>startDate</c> = <c>endDate</c>): <c>Value[]</c> of <c>MetricType</c> (PageView,
/// Acquisition, Install, Usage) and <c>UserCount</c>; the PageView count is the day's page views (users), reported only
/// for every country and source together.</item>
/// </list>
/// </summary>
public static class MicrosoftAnalyticsReports
{
    /// <summary>
    /// Maps a Microsoft <c>storeClient</c>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static TrafficSource Source(string value)
    {
        var text = (value ?? string.Empty).ToLowerInvariant();

        if (text.Contains("(client)", StringComparison.Ordinal))
            return TrafficSource.StoreClient;

        if (text.Contains("(web)", StringComparison.Ordinal))
            return TrafficSource.StoreWeb;

        return text.Length == 0 ? TrafficSource.Unavailable : TrafficSource.Other;
    }

    /// <summary>
    /// Adds one page of an <c>appacquisitions</c> answer.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="accumulator"></param>
    public static void AddAcquisitions(JsonElement root, StoreMetricAccumulator accumulator)
    {
        foreach (var item in Values(root))
        {
            var date = Date(Text(item, "date"));
            var quantity = Number(item, "acquisitionQuantity");

            if (date == null || quantity == null)
                continue;

            var market = Text(item, "market");
            var client = Text(item, "storeClient");

            accumulator.Add(date.Value, Has(item, "market") ? market ?? string.Empty : null, Has(item, "storeClient") ? Source(client) : null, StoreMetricKind.Downloads, quantity.Value);
        }
    }

    /// <summary>
    /// Adds the page views of a one day <c>funnel</c> answer for <paramref name="day"/>.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="day"></param>
    /// <param name="accumulator"></param>
    public static void AddFunnelDay(JsonElement root, DateOnly day, StoreMetricAccumulator accumulator)
    {
        foreach (var item in Values(root))
        {
            if (string.Equals(Text(item, "MetricType"), "PageView", StringComparison.OrdinalIgnoreCase) && Number(item, "UserCount") is { } count)
                accumulator.Add(day, null, null, StoreMetricKind.PageViews, count);
        }
    }

    /// <summary>
    /// The <c>@nextLink</c> of an answer, or null.
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static string NextLink(JsonElement root) => Text(root, "@nextLink");

    private static IEnumerable<JsonElement> Values(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return [];

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals("Value", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Array)
                return property.Value.EnumerateArray();
        }

        return [];
    }

    private static bool Has(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out _);

    private static string Text(JsonElement item, string name)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // UserCount is documented as a string and returned as a number; both are read.
    private static long? Number(JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt64(out var whole) ? whole : (long)Math.Round(value.GetDouble());

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? (long)Math.Round(parsed) : null;
    }

    private static DateOnly? Date(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date) ? DateOnly.FromDateTime(date) : null;
    }
}
