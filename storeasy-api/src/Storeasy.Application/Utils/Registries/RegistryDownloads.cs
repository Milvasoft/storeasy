using Storeasy.Domain.Registries;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// Downloads and usage of a package as its registry reports them. Every value is optional: a registry gives what it has.
/// </summary>
public sealed class RegistryDownloadsRead
{
    /// <summary>All time downloads.</summary>
    public long? Total { get; set; }

    /// <summary>Downloads in the last <see cref="RecentDays"/> days.</summary>
    public long? Recent { get; set; }

    /// <summary>Window of <see cref="Recent"/>.</summary>
    public int? RecentDays { get; set; }

    /// <summary>Daily downloads the registry gives (npm, PyPI, crates.io).</summary>
    public Dictionary<DateTime, long> Days { get; } = [];

    /// <summary>Downloads per version.</summary>
    public Dictionary<string, long> Versions { get; } = new(StringComparer.Ordinal);

    /// <summary>What <see cref="Versions"/> covers.</summary>
    public DownloadScope? VersionScope { get; set; }

    /// <summary>Likes (pub.dev) or favers (Packagist).</summary>
    public long? Likes { get; set; }

    /// <summary>pub.dev points granted.</summary>
    public int? Points { get; set; }

    /// <summary>pub.dev points possible.</summary>
    public int? MaxPoints { get; set; }
}

/// <summary>
/// What saving one package's downloads changes in the database.
/// </summary>
public sealed class DownloadsOutcome
{
    /// <summary>Day rows to insert.</summary>
    public List<PackageDownloadDay> AddedDays { get; } = [];

    /// <summary>Day rows whose values changed.</summary>
    public List<PackageDownloadDay> UpdatedDays { get; } = [];

    /// <summary>Known versions whose download count changed.</summary>
    public List<TrackedPackageVersion> UpdatedVersions { get; } = [];
}

/// <summary>
/// Reads the download answers of the registries (docs/package-registries.md) and turns them into storeasy's rows: the
/// package's totals, per version counts and one row per day (the registry's daily count, and a snapshot of the all time
/// total so a trend exists where the registry gives totals only).
/// </summary>
public static class RegistryDownloads
{
    /// <summary>
    /// Days of daily counts kept from one read.
    /// </summary>
    public const int MaxDays = 180;

    /// <summary>
    /// NuGet search (<c>SearchQueryService</c>, <c>q=packageid:&lt;id&gt;</c>): <c>data[].totalDownloads</c> and
    /// <c>data[].versions[]</c> with <c>version</c> and <c>downloads</c> (all time).
    /// </summary>
    public static RegistryDownloadsRead ParseNuGetSearch(string json, string id)
    {
        var item = (Parse(json)?["data"] as JsonArray ?? []).FirstOrDefault(d => string.Equals(Text(d?["id"]), id, StringComparison.OrdinalIgnoreCase));
        var read = new RegistryDownloadsRead { Total = Long(item?["totalDownloads"]), VersionScope = DownloadScope.Total };

        foreach (var version in item?["versions"] as JsonArray ?? [])
        {
            if (Text(version?["version"]) is string number && Long(version["downloads"]) is long downloads)
                read.Versions[number] = downloads;
        }

        return read;
    }

    /// <summary>
    /// npm: the range answer (<c>api.npmjs.org/downloads/range/&lt;start&gt;:&lt;end&gt;/&lt;name&gt;</c>,
    /// <c>downloads[]</c> with <c>day</c> and <c>downloads</c>) and the per version answer of the last 7 days
    /// (<c>api.npmjs.org/versions/&lt;encoded name&gt;/last-week</c>, <c>downloads</c> keyed by version). npm gives no all
    /// time total; the last 30 days are the recent count.
    /// </summary>
    public static RegistryDownloadsRead ParseNpm(string rangeJson, string versionsJson, DateTime today)
    {
        var read = new RegistryDownloadsRead { VersionScope = DownloadScope.LastWeek, RecentDays = 30 };

        foreach (var day in Parse(rangeJson)?["downloads"] as JsonArray ?? [])
            AddDay(read, Text(day?["day"]), Long(day?["downloads"]));

        if (Parse(versionsJson)?["downloads"] is JsonObject versions)
        {
            foreach (var (version, value) in versions)
            {
                if (Long(value) is long downloads)
                    read.Versions[version] = downloads;
            }
        }

        read.Recent = SumSince(read, today, 30);

        return read;
    }

    /// <summary>
    /// PyPI through pypistats.org: <c>/recent</c> (<c>data.last_month</c>) and <c>/overall?mirrors=false</c>
    /// (<c>data[]</c> with <c>category</c> <c>without_mirrors</c>, <c>date</c>, <c>downloads</c>; 180 days). No all time
    /// total and no per version counts.
    /// </summary>
    public static RegistryDownloadsRead ParsePyPIStats(string recentJson, string overallJson)
    {
        var read = new RegistryDownloadsRead { Recent = Long(Parse(recentJson)?["data"]?["last_month"]), RecentDays = 30 };

        foreach (var day in (Parse(overallJson)?["data"] as JsonArray ?? []).Where(d => Text(d?["category"]) == "without_mirrors"))
            AddDay(read, Text(day?["date"]), Long(day?["downloads"]));

        return read;
    }

    /// <summary>
    /// crates.io: the crate (<c>crate.downloads</c>, <c>crate.recent_downloads</c> of 90 days, <c>versions[]</c> with
    /// <c>id</c>, <c>num</c> and <c>downloads</c>) and <c>/downloads</c> (<c>version_downloads[]</c> with the version's
    /// numeric <c>version</c> id, <c>date</c> and <c>downloads</c>, and <c>meta.extra_downloads[]</c> for older versions),
    /// summed per day.
    /// </summary>
    public static RegistryDownloadsRead ParseCrate(string crateJson, string downloadsJson)
    {
        var crate = Parse(crateJson);
        var read = new RegistryDownloadsRead
        {
            Total = Long(crate?["crate"]?["downloads"]),
            Recent = Long(crate?["crate"]?["recent_downloads"]),
            RecentDays = 90,
            VersionScope = DownloadScope.Total,
        };

        foreach (var version in crate?["versions"] as JsonArray ?? [])
        {
            if (Text(version?["num"]) is string number && Long(version["downloads"]) is long downloads)
                read.Versions[number] = downloads;
        }

        var daily = Parse(downloadsJson);
        var rows = (daily?["version_downloads"] as JsonArray ?? []).Concat(daily?["meta"]?["extra_downloads"] as JsonArray ?? []);

        foreach (var row in rows)
            AddDay(read, Text(row?["date"]), Long(row?["downloads"]), add: true);

        return read;
    }

    /// <summary>
    /// RubyGems: <c>downloads</c> of the gem and <c>downloads_count</c> of each version (summed over its platforms).
    /// </summary>
    public static RegistryDownloadsRead ParseRubyGems(string gemJson, string versionsJson)
    {
        var read = new RegistryDownloadsRead { Total = Long(Parse(gemJson)?["downloads"]), VersionScope = DownloadScope.Total };

        foreach (var version in Parse(versionsJson) as JsonArray ?? [])
        {
            if (Text(version?["number"]) is string number && Long(version["downloads_count"]) is long downloads)
                read.Versions[number] = read.Versions.GetValueOrDefault(number) + downloads;
        }

        return read;
    }

    /// <summary>
    /// Packagist (<c>packagist.org/packages/&lt;vendor&gt;/&lt;package&gt;.json</c>): <c>package.downloads.total</c>,
    /// <c>.monthly</c> and <c>package.favers</c>.
    /// </summary>
    public static RegistryDownloadsRead ParsePackagist(string json)
    {
        var package = Parse(json)?["package"];

        return new RegistryDownloadsRead
        {
            Total = Long(package?["downloads"]?["total"]),
            Recent = Long(package?["downloads"]?["monthly"]),
            RecentDays = 30,
            Likes = Long(package?["favers"]),
        };
    }

    /// <summary>
    /// pub.dev score (<c>/api/packages/&lt;name&gt;/score</c>): <c>downloadCount30Days</c>, <c>likeCount</c>,
    /// <c>grantedPoints</c> and <c>maxPoints</c>.
    /// </summary>
    public static RegistryDownloadsRead ParsePubScore(string json)
    {
        var score = Parse(json);

        return new RegistryDownloadsRead
        {
            Recent = Long(score?["downloadCount30Days"]),
            RecentDays = 30,
            Likes = Long(score?["likeCount"]),
            Points = (int?)Long(score?["grantedPoints"]),
            MaxPoints = (int?)Long(score?["maxPoints"]),
        };
    }

    /// <summary>
    /// Saves a read into <paramref name="package"/>, its <paramref name="versions"/> and its day rows: today's row gets the
    /// all time total, every day the registry counted gets its count. Only rows that change are returned.
    /// </summary>
    public static DownloadsOutcome Apply(TrackedPackage package, RegistryDownloadsRead read, IReadOnlyCollection<TrackedPackageVersion> versions, IReadOnlyCollection<PackageDownloadDay> days, DateTime now)
    {
        var outcome = new DownloadsOutcome();

        package.TotalDownloads = read.Total;
        package.RecentDownloads = read.Recent;
        package.RecentDownloadsDays = read.Recent == null ? null : read.RecentDays;
        package.VersionDownloadsScope = read.Versions.Count == 0 ? null : read.VersionScope;
        package.Likes = read.Likes;
        package.Points = read.Points;
        package.MaxPoints = read.MaxPoints;
        package.DownloadsSyncedDate = now;

        foreach (var version in versions)
        {
            long? downloads = read.Versions.TryGetValue(version.Version, out var value) ? value : null;

            if (version.Downloads == downloads)
                continue;

            version.Downloads = downloads;

            if (version.Id != 0)
                outcome.UpdatedVersions.Add(version);
        }

        var known = days.GroupBy(d => d.Day.Date).ToDictionary(g => g.Key, g => g.First());
        var today = now.Date;
        var counts = read.Days.Where(d => d.Key > today.AddDays(-MaxDays) && d.Key <= today).ToDictionary(d => d.Key, d => (long?)d.Value);

        if (read.Total != null || counts.ContainsKey(today))
            counts.TryAdd(today, null);

        foreach (var (day, count) in counts)
        {
            var total = day == today ? read.Total : null;

            if (!known.TryGetValue(day, out var row))
            {
                outcome.AddedDays.Add(new PackageDownloadDay { OrganizationId = package.OrganizationId, TrackedPackageId = package.Id, Day = day, Downloads = count, TotalDownloads = total });
                continue;
            }

            var downloads = count ?? row.Downloads;
            var snapshot = total ?? row.TotalDownloads;

            if (row.Downloads == downloads && row.TotalDownloads == snapshot)
                continue;

            row.Downloads = downloads;
            row.TotalDownloads = snapshot;
            outcome.UpdatedDays.Add(row);
        }

        return outcome;
    }

    /// <summary>
    /// The daily series of a package for a chart: the registry's count of a day, or else the growth of the all time total
    /// since the day before (the snapshots). Days without either are left out.
    /// </summary>
    public static List<(DateTime Day, long Downloads)> Series(IEnumerable<PackageDownloadDay> days)
    {
        var ordered = days.OrderBy(d => d.Day).ToList();
        var series = new List<(DateTime Day, long Downloads)>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var day = ordered[i];

            if (day.Downloads is long count)
            {
                series.Add((day.Day.Date, count));
                continue;
            }

            var before = i > 0 ? ordered[i - 1] : null;

            if (day.TotalDownloads is long total && before?.TotalDownloads is long previous && before.Day.Date == day.Day.Date.AddDays(-1) && total >= previous)
                series.Add((day.Day.Date, total - previous));
        }

        return series;
    }

    private static void AddDay(RegistryDownloadsRead read, string date, long? downloads, bool add = false)
    {
        if (downloads == null || !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var day))
            return;

        day = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        read.Days[day] = add ? read.Days.GetValueOrDefault(day) + downloads.Value : downloads.Value;
    }

    private static long? SumSince(RegistryDownloadsRead read, DateTime today, int days)
        => read.Days.Count == 0 ? null : read.Days.Where(d => d.Key > today.Date.AddDays(-days)).Sum(d => d.Value);

    private static JsonNode Parse(string json) => RegistryParsers.Parse(json);

    private static string Text(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static long? Long(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<long>(out var number) ? number : null;
}
