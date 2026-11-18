using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storeasy.Domain.Rules;

/// <summary>
/// The loaded store rules catalog (<c>rules/</c> in the repository, docs/urun-plani.md section 12): the manifest version and
/// every rule of every store file. Immutable after loading; readers share one instance.
/// </summary>
/// <param name="version">Catalog version of the manifest (every store file carries the same one).</param>
/// <param name="generatedAt">UTC instant the catalog data was generated.</param>
/// <param name="rules">Rules of every store file, in file order.</param>
/// <param name="source">Where the catalog was loaded from (folder path or "embedded").</param>
public sealed class RuleCatalog(string version, DateTime generatedAt, IReadOnlyList<CatalogRule> rules, string source)
{
    private readonly Dictionary<string, CatalogRule> _byId = rules.ToDictionary(r => r.Id, StringComparer.Ordinal);

    /// <summary>
    /// Catalog version (semver like).
    /// </summary>
    public string Version { get; } = version;

    /// <summary>
    /// UTC instant the catalog data was generated.
    /// </summary>
    public DateTime GeneratedAt { get; } = generatedAt;

    /// <summary>
    /// All rules.
    /// </summary>
    public IReadOnlyList<CatalogRule> Rules { get; } = rules;

    /// <summary>
    /// Where the catalog was loaded from.
    /// </summary>
    public string Source { get; } = source;

    /// <summary>
    /// The rule with <paramref name="id"/>, or null.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public CatalogRule Find(string id) => id != null && _byId.TryGetValue(id, out var rule) ? rule : null;

    /// <summary>
    /// Rules of one store (deprecated rules excluded unless asked for).
    /// </summary>
    /// <param name="store"></param>
    /// <param name="includeDeprecated"></param>
    /// <returns></returns>
    public IEnumerable<CatalogRule> ForStore(Enums.StoreKind store, bool includeDeprecated = false)
        => Rules.Where(r => r.StoreKind == store && (includeDeprecated || r.Deprecated == null));
}

/// <summary>
/// <c>catalog.json</c>: the manifest of a catalog.
/// </summary>
public sealed class CatalogManifest
{
    /// <summary>
    /// Always "manifest".
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Catalog version.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// UTC instant the data was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// License of the data.
    /// </summary>
    public string License { get; set; }

    /// <summary>
    /// Store files of the catalog.
    /// </summary>
    public List<CatalogFileReference> Files { get; set; }
}

/// <summary>
/// One store file listed in the manifest.
/// </summary>
public sealed class CatalogFileReference
{
    /// <summary>
    /// Store key (apple, google, microsoft).
    /// </summary>
    public string Store { get; set; }

    /// <summary>
    /// File name relative to the catalog folder.
    /// </summary>
    public string Path { get; set; }
}

/// <summary>
/// One store's rules file (<c>apple.json</c>, <c>google.json</c>, <c>microsoft.json</c>).
/// </summary>
public sealed class StoreRulesFile
{
    /// <summary>
    /// Always "store-rules".
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Catalog version; must equal the version in the manifest.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Store key.
    /// </summary>
    public string Store { get; set; }

    /// <summary>
    /// UTC instant the data was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Rules of the store.
    /// </summary>
    public List<CatalogRule> Rules { get; set; }
}

/// <summary>
/// One store rule: what it applies to, what it requires, until when, and where it comes from.
/// </summary>
public sealed class CatalogRule
{
    /// <summary>
    /// Stable id: <c>store.category.slug</c>.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Store key (apple, google, microsoft).
    /// </summary>
    public string Store { get; set; }

    /// <summary>
    /// Category (screenshots, app-previews, graphics, listing-limits, age-rating, compliance, privacy, sdk, target-api...).
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Title in English and Turkish.
    /// </summary>
    public RuleText Title { get; set; }

    /// <summary>
    /// Description in English (and optionally Turkish).
    /// </summary>
    public RuleText Description { get; set; }

    /// <summary>
    /// Conditions on the app; null means the rule applies to every app of the store.
    /// </summary>
    public RuleConditions AppliesWhen { get; set; }

    /// <summary>
    /// What the rule requires.
    /// </summary>
    public RuleRequirement Requirement { get; set; }

    /// <summary>
    /// Day the rule started or starts to apply.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Day by which affected apps must comply.
    /// </summary>
    public DateOnly? Deadline { get; set; }

    /// <summary>
    /// Source wording when the date is approximate.
    /// </summary>
    public string DateNote { get; set; }

    /// <summary>
    /// Machine checkable values.
    /// </summary>
    public RuleParams Params { get; set; }

    /// <summary>
    /// How the rule is checked.
    /// </summary>
    public RuleCheck Check { get; set; }

    /// <summary>
    /// Official sources.
    /// </summary>
    public List<RuleSource> Sources { get; set; }

    /// <summary>
    /// Whether every fact was confirmed against the sources.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// Notes (what could not be confirmed, store behaviour).
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Documented disagreements between official sources.
    /// </summary>
    public List<RuleConflict> Conflicts { get; set; }

    /// <summary>
    /// Set when the rule is retired.
    /// </summary>
    public RuleDeprecation Deprecated { get; set; }

    /// <summary>
    /// <see cref="Store"/> as a store kind; null for an unknown key.
    /// </summary>
    [JsonIgnore]
    public Enums.StoreKind? StoreKind => RuleStores.Parse(Store);
}

/// <summary>
/// A text in English and Turkish.
/// </summary>
public sealed class RuleText
{
    /// <summary>
    /// English text.
    /// </summary>
    public string En { get; set; }

    /// <summary>
    /// Turkish text (optional for descriptions).
    /// </summary>
    public string Tr { get; set; }

    /// <summary>
    /// The text in <paramref name="language"/> ("tr" picks Turkish when present); English otherwise.
    /// </summary>
    /// <param name="language"></param>
    /// <returns></returns>
    public string In(string language)
        => language != null && language.StartsWith("tr", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Tr) ? Tr : En;
}

/// <summary>
/// Conditions on the app. Keys are combined with AND, values of one list with OR; a missing key does not restrict.
/// </summary>
public sealed class RuleConditions
{
    /// <summary>
    /// Platforms (ios, ipados, android, wear-os...).
    /// </summary>
    public List<string> Platforms { get; set; }

    /// <summary>
    /// Device families the app supports (iphone, ipad, phone, tablet-7...).
    /// </summary>
    public List<string> DeviceFamilies { get; set; }

    /// <summary>
    /// Distribution regions (ISO 3166-1 alpha-2).
    /// </summary>
    public List<string> Regions { get; set; }

    /// <summary>
    /// Developer account regions.
    /// </summary>
    public List<string> DeveloperRegions { get; set; }

    /// <summary>
    /// App types (app, game).
    /// </summary>
    public List<string> AppTypes { get; set; }

    /// <summary>
    /// Package types (ipa, aab, apk, msix...).
    /// </summary>
    public List<string> PackageTypes { get; set; }

    /// <summary>
    /// Submission situations (new-app, update, existing-app).
    /// </summary>
    public List<string> Distribution { get; set; }

    /// <summary>
    /// App features (storeasy feature flags kept per app).
    /// </summary>
    public List<string> Features { get; set; }

    /// <summary>
    /// Constraints read from the build.
    /// </summary>
    public RuleBuildCondition Build { get; set; }

    /// <summary>
    /// Human readable summary.
    /// </summary>
    public string Expression { get; set; }
}

/// <summary>
/// Build constraint of a rule.
/// </summary>
public sealed class RuleBuildCondition
{
    /// <summary>
    /// SDK name (iOS, macOS...).
    /// </summary>
    public string SdkName { get; set; }

    /// <summary>
    /// Smallest SDK version the rule applies to.
    /// </summary>
    public string SdkVersionAtLeast { get; set; }
}

/// <summary>
/// Requirement level of a rule.
/// </summary>
public sealed class RuleRequirement
{
    /// <summary>
    /// Requirement level of a required rule.
    /// </summary>
    public const string Required = "required";

    /// <summary>
    /// Requirement level of a rule that becomes required on <see cref="From"/>.
    /// </summary>
    public const string RequiredFrom = "required-from";

    /// <summary>
    /// Requirement level of a recommended rule.
    /// </summary>
    public const string Recommended = "recommended";

    /// <summary>
    /// Requirement level of an optional rule.
    /// </summary>
    public const string Optional = "optional";

    /// <summary>
    /// required, required-from, recommended or optional.
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// Day a required-from rule becomes required.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Rules the store derives a missing asset from, in order.
    /// </summary>
    public List<string> FallbackTo { get; set; }

    /// <summary>
    /// Rules that also meet the requirement.
    /// </summary>
    public List<string> SatisfiedBy { get; set; }

    /// <summary>
    /// Wording of the condition.
    /// </summary>
    public string Condition { get; set; }
}

/// <summary>
/// Machine checkable values of a rule. Known keys are typed; other keys stay in <see cref="Extra"/>.
/// </summary>
public sealed class RuleParams
{
    /// <summary>
    /// Accepted pixel sizes (exact match).
    /// </summary>
    public List<RuleSize> Sizes { get; set; }

    /// <summary>
    /// Accepted file formats.
    /// </summary>
    public List<string> Formats { get; set; }

    /// <summary>
    /// Whether an alpha channel is accepted.
    /// </summary>
    public bool? AlphaAllowed { get; set; }

    /// <summary>
    /// Fewest items.
    /// </summary>
    public int? MinCount { get; set; }

    /// <summary>
    /// Most items.
    /// </summary>
    public int? MaxCount { get; set; }

    /// <summary>
    /// Fewest characters.
    /// </summary>
    public int? MinChars { get; set; }

    /// <summary>
    /// Most characters.
    /// </summary>
    public int? MaxChars { get; set; }

    /// <summary>
    /// Most UTF-8 bytes.
    /// </summary>
    public int? MaxBytes { get; set; }

    /// <summary>
    /// Smallest side in pixels.
    /// </summary>
    public int? MinDimension { get; set; }

    /// <summary>
    /// Largest side in pixels.
    /// </summary>
    public int? MaxDimension { get; set; }

    /// <summary>
    /// Largest long side / short side ratio.
    /// </summary>
    public double? MaxAspectRatio { get; set; }

    /// <summary>
    /// Largest file in bytes.
    /// </summary>
    public long? MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Smallest target SDK (Android API level).
    /// </summary>
    public int? MinTargetSdk { get; set; }

    /// <summary>
    /// Smallest Xcode version.
    /// </summary>
    public string MinXcodeVersion { get; set; }

    /// <summary>
    /// Smallest SDK version.
    /// </summary>
    public string MinSdkVersion { get; set; }

    /// <summary>
    /// Smallest deployment target.
    /// </summary>
    public string MinDeploymentTarget { get; set; }

    /// <summary>
    /// Most days (e.g. TestFlight build lifetime).
    /// </summary>
    public int? MaxDays { get; set; }

    /// <summary>
    /// Days before a date to warn.
    /// </summary>
    public int? WarnDaysBefore { get; set; }

    /// <summary>
    /// Most items of a list field (e.g. Microsoft product features, search terms).
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    /// Most characters of each item of a list field.
    /// </summary>
    public int? MaxCharsPerItem { get; set; }

    /// <summary>
    /// Most screenshots of the other device families (Microsoft Store: Xbox, holographic...).
    /// </summary>
    public int? MaxCountOtherFamilies { get; set; }

    /// <summary>
    /// Most characters of an image caption.
    /// </summary>
    public int? CaptionMaxChars { get; set; }

    /// <summary>
    /// Other, store specific values.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; }
}

/// <summary>
/// A pixel size.
/// </summary>
public sealed class RuleSize
{
    /// <summary>
    /// Width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Note on the size.
    /// </summary>
    public string Note { get; set; }
}

/// <summary>
/// How a rule is checked.
/// </summary>
public sealed class RuleCheck
{
    /// <summary>
    /// Kind of an automatic check.
    /// </summary>
    public const string Automatic = "automatic";

    /// <summary>
    /// Kind of an assisted (checklist) check.
    /// </summary>
    public const string Assisted = "assisted";

    /// <summary>
    /// Kind of a timeline only rule.
    /// </summary>
    public const string Info = "info";

    /// <summary>
    /// automatic, assisted or info.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Data keys the check needs.
    /// </summary>
    public List<string> Inputs { get; set; }
}

/// <summary>
/// An official source of a rule.
/// </summary>
public sealed class RuleSource
{
    /// <summary>
    /// Address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Day the source was checked.
    /// </summary>
    public DateOnly CheckedAt { get; set; }

    /// <summary>
    /// Short supporting excerpt.
    /// </summary>
    public string Quote { get; set; }
}

/// <summary>
/// A disagreement between official sources and the reading the catalog uses.
/// </summary>
public sealed class RuleConflict
{
    /// <summary>
    /// What disagrees.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Disagreeing sources.
    /// </summary>
    public List<string> Sources { get; set; }

    /// <summary>
    /// Reading the catalog encodes.
    /// </summary>
    public string CatalogChoice { get; set; }
}

/// <summary>
/// Retirement of a rule.
/// </summary>
public sealed class RuleDeprecation
{
    /// <summary>
    /// Catalog version the rule was retired in.
    /// </summary>
    public string Since { get; set; }

    /// <summary>
    /// Rule that replaces it.
    /// </summary>
    public string ReplacedBy { get; set; }

    /// <summary>
    /// Why.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// Store keys of the catalog.
/// </summary>
public static class RuleStores
{
    /// <summary>
    /// Catalog key of App Store Connect.
    /// </summary>
    public const string Apple = "apple";

    /// <summary>
    /// Catalog key of Google Play.
    /// </summary>
    public const string Google = "google";

    /// <summary>
    /// Catalog key of Microsoft Store.
    /// </summary>
    public const string Microsoft = "microsoft";

    /// <summary>
    /// Store kind of a catalog key; null for an unknown key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static Enums.StoreKind? Parse(string key) => key switch
    {
        Apple => Enums.StoreKind.Apple,
        Google => Enums.StoreKind.Google,
        Microsoft => Enums.StoreKind.Microsoft,
        _ => null,
    };

    /// <summary>
    /// Catalog key of a store kind.
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static string Key(Enums.StoreKind store) => store switch
    {
        Enums.StoreKind.Apple => Apple,
        Enums.StoreKind.Google => Google,
        _ => Microsoft,
    };
}
