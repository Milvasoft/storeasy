using Storeasy.Domain.Registries;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// Kinds of changes a sync reports (audit entry and notification).
/// </summary>
public enum RegistryChangeKind
{
    /// <summary>A version storeasy had not seen.</summary>
    NewVersion,

    /// <summary>A known version became deprecated.</summary>
    Deprecated,

    /// <summary>A known version was yanked or unlisted.</summary>
    Yanked,

    /// <summary>A security advisory now affects the latest version.</summary>
    Advisory,
}

/// <summary>
/// One change a sync found.
/// </summary>
/// <param name="Kind">What changed.</param>
/// <param name="Version">The version.</param>
/// <param name="Detail">Deprecation message, yank reason or advisory id and summary.</param>
public sealed record RegistryChange(RegistryChangeKind Kind, string Version, string Detail);

/// <summary>
/// An alert the rules want open.
/// </summary>
/// <param name="Kind">Kind.</param>
/// <param name="Version">Version.</param>
/// <param name="AdvisoryId">Advisory id of an advisory alert.</param>
/// <param name="Severity">Advisory severity.</param>
/// <param name="Summary">One line.</param>
/// <param name="Url">Details address.</param>
public sealed record AlertCandidate(PackageAlertKind Kind, string Version, string AdvisoryId, string Severity, string Summary, string Url);

/// <summary>
/// What one sync of one package changes in the database.
/// </summary>
public sealed class RegistrySyncOutcome
{
    /// <summary>Versions to insert.</summary>
    public List<TrackedPackageVersion> AddedVersions { get; } = [];

    /// <summary>Known versions whose values changed.</summary>
    public List<TrackedPackageVersion> UpdatedVersions { get; } = [];

    /// <summary>Alerts to insert.</summary>
    public List<PackageAlert> OpenedAlerts { get; } = [];

    /// <summary>Open alerts whose condition is gone (their resolved date is set).</summary>
    public List<PackageAlert> ResolvedAlerts { get; } = [];

    /// <summary>Changes to audit and notify.</summary>
    public List<RegistryChange> Changes { get; } = [];
}

/// <summary>
/// Merges a registry read into a tracked package and decides its alerts (docs/package-registries.md):
/// <list type="bullet">
/// <item><b>Deprecated</b>: the latest version is deprecated, or a version was deprecated after the first sync; open while
/// the version stays deprecated.</item>
/// <item><b>Yanked</b>: a version was yanked or unlisted after the first sync; open while it stays so.</item>
/// <item><b>NoProvenance</b>: the latest version has no provenance (never for NuGet, which has none).</item>
/// <item><b>Advisory</b>: OSV.dev reports an advisory for the latest version; when OSV.dev could not be read the open
/// advisory alerts are kept as they are.</item>
/// </list>
/// The first sync of a package reports no version change (only newly opened advisories); later syncs report new versions,
/// newly deprecated and yanked versions and newly opened advisories.
/// </summary>
public static class RegistrySyncRules
{
    /// <summary>
    /// Most new versions one sync reports as changes (the newest; every version is still saved).
    /// </summary>
    public const int MaxReportedNewVersions = 5;

    /// <summary>
    /// Identity of an alert.
    /// </summary>
    public static string AlertKey(PackageAlertKind kind, string version, string advisoryId) => $"{kind}|{version}|{advisoryId}";

    /// <summary>
    /// Applies provenance reads (version to provenance; a null value means the version was checked and has none) to the read.
    /// </summary>
    public static void ApplyProvenance(RegistryPackageRead read, IReadOnlyDictionary<string, ProvenanceRead> provenance)
    {
        foreach (var version in read.Versions)
        {
            if (!provenance.TryGetValue(version.Version, out var value))
                continue;

            version.HasProvenance = value != null;
            version.CiRunUrl = value?.RunUrl ?? version.CiRunUrl;
            version.SourceRepository = value?.Repository ?? version.SourceRepository;
        }
    }

    /// <summary>
    /// Merges <paramref name="read"/> into <paramref name="package"/> and its <paramref name="versions"/>, and decides the
    /// alerts against the <paramref name="openAlerts"/>. <paramref name="advisories"/> null means OSV.dev was not read.
    /// </summary>
    public static RegistrySyncOutcome Merge(TrackedPackage package,
                                            RegistryPackageRead read,
                                            string latestWorkflow,
                                            IReadOnlyCollection<TrackedPackageVersion> versions,
                                            IReadOnlyCollection<PackageAlert> openAlerts,
                                            IReadOnlyList<RegistryAdvisory> advisories,
                                            DateTime now,
                                            int maxVersions)
    {
        var outcome = new RegistrySyncOutcome();
        var known = versions.GroupBy(v => v.Version, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var firstSync = package.SyncedDate == null && known.Count == 0;
        // The flags before this sync: merging updates the known rows in place.
        var before = known.ToDictionary(pair => pair.Key, pair => (pair.Value.IsDeprecated, Yanked: pair.Value.IsYanked || pair.Value.IsUnlisted), StringComparer.Ordinal);
        var kept = read.Versions.Where(v => !string.IsNullOrEmpty(v.Version) && v.Version.Length <= TrackedPackage.VersionMaxLength)
                                .OrderByDescending(v => v.Version == read.LatestVersion)
                                .ThenByDescending(v => v.Published ?? DateTime.MinValue)
                                .Take(Math.Max(1, maxVersions))
                                .ToList();

        foreach (var version in kept)
            MergeVersion(package, version, known, firstSync, outcome);

        var older = outcome.Changes.Where(c => c.Kind == RegistryChangeKind.NewVersion).Skip(MaxReportedNewVersions).ToHashSet();

        outcome.Changes.RemoveAll(older.Contains);

        var latest = read.Latest;
        var latestRow = latest == null ? null : known.GetValueOrDefault(latest.Version) ?? outcome.AddedVersions.Find(v => v.Version == latest.Version);

        package.Name = string.IsNullOrEmpty(read.Name) ? package.Name : Trim(read.Name, TrackedPackage.NameMaxLength);
        package.LatestVersion = Trim(read.LatestVersion, TrackedPackage.VersionMaxLength);
        package.LatestPublishedDate = latest?.Published;
        package.HasProvenance = RegistryNames.SupportsProvenance(package.Registry) && latestRow?.HasProvenance == true;
        package.LatestCiRunUrl = package.HasProvenance ? Trim(latestRow?.CiRunUrl, TrackedPackage.UrlMaxLength) : null;
        package.LatestWorkflow = package.HasProvenance ? Trim(latestWorkflow, TrackedPackage.UrlMaxLength) : null;
        package.SourceRepository = Trim((package.HasProvenance ? latest?.SourceRepository : null) ?? read.Repository, TrackedPackage.UrlMaxLength);
        package.DeprecationMessage = latest?.Deprecated == true ? Trim(latest.Message ?? string.Empty, TrackedPackage.MessageMaxLength) : null;

        var desired = Alerts(package, read, before, latestRow, openAlerts, advisories);
        var open = openAlerts.GroupBy(a => AlertKey(a.Kind, a.Version, a.AdvisoryId)).ToDictionary(g => g.Key, g => g.First());

        foreach (var candidate in desired.Where(c => !open.ContainsKey(KeyOf(c))))
        {
            outcome.OpenedAlerts.Add(new PackageAlert
            {
                OrganizationId = package.OrganizationId,
                TrackedPackageId = package.Id,
                Kind = candidate.Kind,
                Version = Trim(candidate.Version, TrackedPackage.VersionMaxLength),
                AdvisoryId = Trim(candidate.AdvisoryId, PackageAlert.AdvisoryIdMaxLength),
                Severity = Trim(candidate.Severity, PackageAlert.SeverityMaxLength),
                Summary = Trim(candidate.Summary, PackageAlert.SummaryMaxLength),
                Url = Trim(candidate.Url, TrackedPackage.UrlMaxLength),
                OpenedDate = now,
            });

            if (candidate.Kind == PackageAlertKind.Advisory)
                outcome.Changes.Add(new RegistryChange(RegistryChangeKind.Advisory, candidate.Version, $"{candidate.AdvisoryId} {candidate.Summary}".Trim()));
        }

        var wanted = desired.Select(KeyOf).ToHashSet();

        foreach (var (key, alert) in open.Where(pair => !wanted.Contains(pair.Key)))
        {
            alert.ResolvedDate = now;
            outcome.ResolvedAlerts.Add(alert);
        }

        return outcome;
    }

    private static void MergeVersion(TrackedPackage package, RegistryVersionRead version, Dictionary<string, TrackedPackageVersion> known, bool firstSync, RegistrySyncOutcome outcome)
    {
        var message = Trim(version.Message, TrackedPackage.MessageMaxLength);

        if (!known.TryGetValue(version.Version, out var row))
        {
            outcome.AddedVersions.Add(new TrackedPackageVersion
            {
                OrganizationId = package.OrganizationId,
                TrackedPackageId = package.Id,
                Version = version.Version,
                PublishedDate = version.Published,
                IsYanked = version.Yanked,
                IsUnlisted = version.Unlisted,
                IsDeprecated = version.Deprecated,
                Message = message,
                HasProvenance = version.HasProvenance,
                CiRunUrl = Trim(version.CiRunUrl, TrackedPackage.UrlMaxLength),
            });

            if (!firstSync)
                outcome.Changes.Add(new RegistryChange(RegistryChangeKind.NewVersion, version.Version, null));

            return;
        }

        if (!firstSync && version.Deprecated && !row.IsDeprecated)
            outcome.Changes.Add(new RegistryChange(RegistryChangeKind.Deprecated, version.Version, message));

        if (!firstSync && (version.Yanked || version.Unlisted) && !(row.IsYanked || row.IsUnlisted))
            outcome.Changes.Add(new RegistryChange(RegistryChangeKind.Yanked, version.Version, message));

        var hasProvenance = version.HasProvenance ?? row.HasProvenance;
        var runUrl = Trim(version.CiRunUrl, TrackedPackage.UrlMaxLength) ?? row.CiRunUrl;

        if (row.PublishedDate == version.Published && row.IsYanked == version.Yanked && row.IsUnlisted == version.Unlisted && row.IsDeprecated == version.Deprecated
            && row.Message == message && row.HasProvenance == hasProvenance && row.CiRunUrl == runUrl)
            return;

        row.PublishedDate = version.Published ?? row.PublishedDate;
        row.IsYanked = version.Yanked;
        row.IsUnlisted = version.Unlisted;
        row.IsDeprecated = version.Deprecated;
        row.Message = message;
        row.HasProvenance = hasProvenance;
        row.CiRunUrl = runUrl;
        outcome.UpdatedVersions.Add(row);
    }

    private static List<AlertCandidate> Alerts(TrackedPackage package,
                                               RegistryPackageRead read,
                                               Dictionary<string, (bool IsDeprecated, bool Yanked)> before,
                                               TrackedPackageVersion latestRow,
                                               IReadOnlyCollection<PackageAlert> openAlerts,
                                               IReadOnlyList<RegistryAdvisory> advisories)
    {
        var openKeys = openAlerts.Select(a => AlertKey(a.Kind, a.Version, a.AdvisoryId)).ToHashSet();
        var candidates = new List<AlertCandidate>();

        foreach (var version in read.Versions)
        {
            var known = before.TryGetValue(version.Version, out var flags);
            var deprecatedKey = AlertKey(PackageAlertKind.Deprecated, version.Version, null);

            if (version.Deprecated && (version.Version == read.LatestVersion || openKeys.Contains(deprecatedKey) || (known && !flags.IsDeprecated)))
                candidates.Add(new AlertCandidate(PackageAlertKind.Deprecated, version.Version, null, null, version.Message, null));

            var yankedKey = AlertKey(PackageAlertKind.Yanked, version.Version, null);

            if ((version.Yanked || version.Unlisted) && (openKeys.Contains(yankedKey) || (known && !flags.Yanked)))
                candidates.Add(new AlertCandidate(PackageAlertKind.Yanked, version.Version, null, null, version.Message, null));
        }

        if (RegistryNames.SupportsProvenance(package.Registry) && latestRow is { HasProvenance: false })
            candidates.Add(new AlertCandidate(PackageAlertKind.NoProvenance, latestRow.Version, null, null, null, null));

        if (advisories == null)
            candidates.AddRange(openAlerts.Where(a => a.Kind == PackageAlertKind.Advisory).Select(a => new AlertCandidate(a.Kind, a.Version, a.AdvisoryId, a.Severity, a.Summary, a.Url)));
        else if (read.LatestVersion != null)
            candidates.AddRange(advisories.Select(a => new AlertCandidate(PackageAlertKind.Advisory, read.LatestVersion, a.Id, a.Severity, a.Summary, a.Url)));

        return [.. candidates.DistinctBy(KeyOf)];
    }

    private static string KeyOf(AlertCandidate candidate) => AlertKey(candidate.Kind, candidate.Version, candidate.AdvisoryId);

    private static string Trim(string value, int maxLength) => value == null ? null : value.Length <= maxLength ? value : value[..maxLength];
}
