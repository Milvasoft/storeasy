using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Rules;
using System.Globalization;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// The catalog checks of a package before delivery (docs/urun-plani.md 11.8 step 3): Android target API and an increasing
/// versionCode, Apple Xcode, SDK and deployment target minimums, a build number not used before and a distribution
/// signature, export compliance. A <see cref="ReadinessStatus.Missing"/> result rejects the package; a
/// <see cref="ReadinessStatus.Warning"/> may hold it (the delivery rule decides). No database, no network.
/// </summary>
public static class ArtifactChecks
{
    /// <summary>
    /// What the checks know besides the package.
    /// </summary>
    /// <param name="Catalog">Rules catalog.</param>
    /// <param name="Today">UTC day of the check.</param>
    /// <param name="KnownVersionCodes">Android: version codes the store or earlier deliveries already have.</param>
    /// <param name="KnownBuilds">Apple: (version, build number) pairs App Store Connect already has.</param>
    public sealed record Context(RuleCatalog Catalog, DateOnly Today, IReadOnlyCollection<long> KnownVersionCodes, IReadOnlyCollection<(string Version, string BuildNumber)> KnownBuilds);

    /// <summary>
    /// Runs the checks of <paramref name="package"/>.
    /// </summary>
    /// <param name="package"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public static List<ArtifactCheck> Run(ArtifactPackageInfo package, Context context) => package.Kind switch
    {
        PackageKind.Aab or PackageKind.Apk => Android(package, context),
        PackageKind.Ipa => Apple(package, context),
        _ => [Check("windows.identity", "Identity", string.IsNullOrWhiteSpace(package.Identifier) ? ReadinessStatus.Missing : ReadinessStatus.Ok, null, "Package identity", package.Identifier)],
    };

    private static List<ArtifactCheck> Android(ArtifactPackageInfo package, Context context)
    {
        var checks = new List<ArtifactCheck>();
        var packageType = package.Kind == PackageKind.Apk ? "apk" : "aab";
        var hasTarget = int.TryParse(package.TargetSdk, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetSdk);

        foreach (var rule in context.Catalog.ForStore(StoreKind.Google).Where(r => r.Category == "target-api" && r.Params?.MinTargetSdk != null && Applies(r, packageType)))
        {
            var required = RuleEvaluator.IsRequiredOn(rule, context.Today);
            var minimum = rule.Params.MinTargetSdk.Value;

            var status = !hasTarget ? ReadinessStatus.Warning
                         : targetSdk >= minimum ? ReadinessStatus.Ok
                         : required ? ReadinessStatus.Missing
                         : ReadinessStatus.Warning;

            checks.Add(Check($"target-api.{rule.Id}", "TargetApi", status, rule.Id, rule.Title?.In("en"), hasTarget ? $"targetSdk {targetSdk}, minimum {minimum}" : "targetSdk not found in the manifest"));
        }

        if (long.TryParse(package.VersionCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var versionCode))
        {
            var highest = context.KnownVersionCodes.Count == 0 ? (long?)null : context.KnownVersionCodes.Max();

            checks.Add(Check("version-code", "Build",
                             highest == null || versionCode > highest ? ReadinessStatus.Ok : ReadinessStatus.Missing,
                             null,
                             "versionCode is higher than any earlier one",
                             highest == null ? $"versionCode {versionCode}, first build" : versionCode > highest ? $"versionCode {versionCode} > {highest}" : $"versionCode {versionCode} is not above {highest}"));
        }
        else
        {
            checks.Add(Check("version-code", "Build", ReadinessStatus.Missing, null, "versionCode is higher than any earlier one", "versionCode not found in the manifest"));
        }

        if (package.Kind == PackageKind.Aab)
            checks.Add(Check("signature", "Signature", package.Signature == "unsigned" ? ReadinessStatus.Missing : ReadinessStatus.Ok, null, "Bundle is signed with the upload key", package.Signature));

        return checks;
    }

    private static List<ArtifactCheck> Apple(ArtifactPackageInfo package, Context context)
    {
        var checks = new List<ArtifactCheck>();

        foreach (var rule in context.Catalog.ForStore(StoreKind.Apple).Where(r => Applies(r, "ipa") && (r.Params?.MinXcodeVersion != null || r.Params?.MinSdkVersion != null || r.Params?.MinDeploymentTarget != null)))
        {
            var required = RuleEvaluator.IsRequiredOn(rule, context.Today);
            var problems = new List<string>();
            var unknown = false;

            Compare(rule.Params.MinXcodeVersion, package.XcodeVersion, "Xcode", problems, ref unknown);
            Compare(rule.Params.MinSdkVersion, package.SdkVersion, "SDK", problems, ref unknown);
            Compare(rule.Params.MinDeploymentTarget, package.MinOs, "deployment target", problems, ref unknown);

            var status = problems.Count > 0 ? (required ? ReadinessStatus.Missing : ReadinessStatus.Warning) : unknown ? ReadinessStatus.Warning : ReadinessStatus.Ok;

            checks.Add(Check($"sdk.{rule.Id}", "Sdk", status, rule.Id, rule.Title?.In("en"), problems.Count > 0 ? string.Join("; ", problems) : $"Xcode {package.XcodeVersion}, SDK {package.SdkVersion}, minimum OS {package.MinOs}"));
        }

        var used = context.KnownBuilds.Any(b => b.Version == package.VersionName && b.BuildNumber == package.VersionCode);

        checks.Add(Check("build-number", "Build", used ? ReadinessStatus.Missing : ReadinessStatus.Ok, null, "Build number not used before",
                         used ? $"{package.VersionName} ({package.VersionCode}) is already in App Store Connect" : $"{package.VersionName} ({package.VersionCode})"));

        var signature = package.Signature;
        var signatureStatus = signature == null ? ReadinessStatus.Warning : signature.StartsWith("app store", StringComparison.Ordinal) ? ReadinessStatus.Ok : ReadinessStatus.Missing;

        checks.Add(Check("signature", "Signature", signatureStatus, null, "Signed for App Store distribution", signature ?? "No embedded provisioning profile"));

        checks.Add(Check("export-compliance", "ExportCompliance", package.UsesNonExemptEncryption == null ? ReadinessStatus.Warning : ReadinessStatus.Ok, null,
                         "Export compliance declared in Info.plist",
                         package.UsesNonExemptEncryption == null ? "ITSAppUsesNonExemptEncryption is not set; App Store Connect will ask" : $"ITSAppUsesNonExemptEncryption = {package.UsesNonExemptEncryption}"));

        return checks;
    }

    private static void Compare(string minimum, string actual, string what, List<string> problems, ref bool unknown)
    {
        if (minimum == null)
            return;

        if (string.IsNullOrWhiteSpace(actual))
        {
            unknown = true;
            return;
        }

        if (!RuleEvaluator.VersionAtLeast(actual, minimum))
            problems.Add($"{what} {actual} is below {minimum}");
    }

    private static bool Applies(CatalogRule rule, string packageType)
        => rule.Deprecated == null && (rule.AppliesWhen?.PackageTypes == null || rule.AppliesWhen.PackageTypes.Count == 0 || rule.AppliesWhen.PackageTypes.Contains(packageType))
           && (rule.AppliesWhen?.Distribution == null || rule.AppliesWhen.Distribution.Count == 0 || rule.AppliesWhen.Distribution.Contains("update") || rule.AppliesWhen.Distribution.Contains("new-app"));

    private static ArtifactCheck Check(string key, string group, ReadinessStatus status, string ruleId, string title, string message)
        => new() { Key = key, Group = group, Status = status, RuleId = ruleId, Title = title, Message = message };
}
