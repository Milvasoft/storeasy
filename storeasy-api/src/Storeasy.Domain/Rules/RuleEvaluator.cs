using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Rules;

/// <summary>
/// What the catalog needs to know about one app in one store to decide which rules apply. A null list means "not known"
/// (the condition on it evaluates to <see cref="RuleApplicability.Unknown"/>); an empty list means "known to be none".
/// </summary>
public sealed class RuleSubject
{
    /// <summary>
    /// Store the app is evaluated for.
    /// </summary>
    public StoreKind Store { get; init; }

    /// <summary>
    /// Platforms (ios, ipados, android...).
    /// </summary>
    public IReadOnlyCollection<string> Platforms { get; init; }

    /// <summary>
    /// Device families the app supports (iphone, ipad, phone, tablet-7...).
    /// </summary>
    public IReadOnlyCollection<string> DeviceFamilies { get; init; }

    /// <summary>
    /// Distribution regions (ISO 3166-1 alpha-2).
    /// </summary>
    public IReadOnlyCollection<string> Regions { get; init; }

    /// <summary>
    /// Developer account region.
    /// </summary>
    public string DeveloperRegion { get; init; }

    /// <summary>
    /// app or game.
    /// </summary>
    public string AppType { get; init; }

    /// <summary>
    /// Package type (ipa, aab...).
    /// </summary>
    public string PackageType { get; init; }

    /// <summary>
    /// new-app, update or existing-app.
    /// </summary>
    public string Distribution { get; init; }

    /// <summary>
    /// App features (storeasy feature flags kept per app).
    /// </summary>
    public IReadOnlyCollection<string> Features { get; init; }

    /// <summary>
    /// SDK name of the newest build (iOS...).
    /// </summary>
    public string BuildSdkName { get; init; }

    /// <summary>
    /// SDK version of the newest build.
    /// </summary>
    public string BuildSdkVersion { get; init; }
}

/// <summary>
/// Result of matching a rule's conditions against a <see cref="RuleSubject"/>.
/// </summary>
/// <param name="Applicability">Verdict.</param>
/// <param name="Reasons">Matched conditions as <c>key:value</c> (e.g. <c>deviceFamilies:ipad</c>).</param>
/// <param name="UnknownKeys">Conditions that could not be decided because the app data is missing.</param>
public sealed record RuleMatch(RuleApplicability Applicability, IReadOnlyList<string> Reasons, IReadOnlyList<string> UnknownKeys);

/// <summary>
/// Pure evaluation of catalog rules: applicability to an app and timing relative to a day. No I/O; the caller passes
/// the day (from <see cref="TimeProvider"/>) so tests control the clock.
/// </summary>
public static class RuleEvaluator
{
    /// <summary>
    /// Matches <paramref name="rule"/> against <paramref name="subject"/>. A rule of another store never applies.
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="subject"></param>
    /// <returns></returns>
    public static RuleMatch Evaluate(CatalogRule rule, RuleSubject subject)
    {
        if (rule.StoreKind != subject.Store)
            return new RuleMatch(RuleApplicability.DoesNotApply, [], []);

        var conditions = rule.AppliesWhen;

        if (conditions == null)
            return new RuleMatch(RuleApplicability.Applies, [], []);

        var reasons = new List<string>();
        var unknown = new List<string>();
        var fails = false;

        void Check(string key, List<string> expected, IReadOnlyCollection<string> actual)
        {
            if (expected == null || expected.Count == 0)
                return;

            if (actual == null)
            {
                unknown.Add(key);
                return;
            }

            var hit = expected.FirstOrDefault(e => actual.Contains(e, StringComparer.OrdinalIgnoreCase));

            if (hit == null)
                fails = true;
            else
                reasons.Add($"{key}:{hit}");
        }

        Check("platforms", conditions.Platforms, subject.Platforms);
        Check("deviceFamilies", conditions.DeviceFamilies, subject.DeviceFamilies);
        Check("regions", conditions.Regions, subject.Regions);
        Check("developerRegions", conditions.DeveloperRegions, Single(subject.DeveloperRegion));
        Check("appTypes", conditions.AppTypes, Single(subject.AppType));
        Check("packageTypes", conditions.PackageTypes, Single(subject.PackageType));
        Check("distribution", conditions.Distribution, Single(subject.Distribution));
        Check("features", conditions.Features, subject.Features);

        if (conditions.Build != null)
        {
            if (string.IsNullOrWhiteSpace(subject.BuildSdkVersion))
            {
                unknown.Add("build");
            }
            else if ((conditions.Build.SdkName == null || string.Equals(conditions.Build.SdkName, subject.BuildSdkName, StringComparison.OrdinalIgnoreCase))
                     && VersionAtLeast(subject.BuildSdkVersion, conditions.Build.SdkVersionAtLeast))
            {
                reasons.Add($"build:{subject.BuildSdkName} {subject.BuildSdkVersion}");
            }
            else
            {
                fails = true;
            }
        }

        if (fails)
            return new RuleMatch(RuleApplicability.DoesNotApply, reasons, unknown);

        return new RuleMatch(unknown.Count > 0 ? RuleApplicability.Unknown : RuleApplicability.Applies, reasons, unknown);
    }

    /// <summary>
    /// The day that matters for the timeline: the deadline, else the day a required-from rule becomes required, else a
    /// future effective date. Null when the rule has no date ahead or behind worth showing.
    /// </summary>
    /// <param name="rule"></param>
    /// <returns></returns>
    public static DateOnly? DueDate(CatalogRule rule) => rule.Deadline ?? rule.Requirement?.From ?? rule.EffectiveDate;

    /// <summary>
    /// Whether the rule is required on <paramref name="today"/>: required rules from their effective date, required-from
    /// rules from their date. Recommended and optional rules are never required.
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="today"></param>
    /// <returns></returns>
    public static bool IsRequiredOn(CatalogRule rule, DateOnly today) => rule.Requirement?.Level switch
    {
        RuleRequirement.Required => rule.EffectiveDate == null || rule.EffectiveDate <= today,
        RuleRequirement.RequiredFrom => rule.Requirement.From != null && rule.Requirement.From <= today,
        _ => false,
    };

    /// <summary>
    /// State of the rule on <paramref name="today"/>.
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="today"></param>
    /// <returns></returns>
    public static RuleStatus StatusOn(CatalogRule rule, DateOnly today)
    {
        if (rule.Deprecated != null)
            return RuleStatus.Deprecated;

        var start = rule.Requirement?.Level == RuleRequirement.RequiredFrom ? rule.Requirement.From : rule.EffectiveDate;

        return start != null && start > today ? RuleStatus.Upcoming : RuleStatus.InForce;
    }

    /// <summary>
    /// Compares dotted versions numerically ("27.1" &gt;= "27.0.2"). Missing parts count as 0; a non numeric part makes
    /// the comparison false.
    /// </summary>
    /// <param name="version"></param>
    /// <param name="minimum"></param>
    /// <returns></returns>
    public static bool VersionAtLeast(string version, string minimum)
    {
        if (string.IsNullOrWhiteSpace(minimum))
            return true;

        if (string.IsNullOrWhiteSpace(version))
            return false;

        var left = version.Trim().Split('.');
        var right = minimum.Trim().Split('.');

        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var a = i < left.Length ? left[i] : "0";
            var b = i < right.Length ? right[i] : "0";

            if (!int.TryParse(a, out var x) || !int.TryParse(b, out var y))
                return false;

            if (x != y)
                return x > y;
        }

        return true;
    }

    private static IReadOnlyCollection<string> Single(string value) => value == null ? null : [value];
}
