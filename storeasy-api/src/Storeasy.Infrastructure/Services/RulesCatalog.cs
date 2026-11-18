using Storeasy.Application.Interfaces;
using Storeasy.Domain.Rules;

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Loads and validates the store rules catalog once, at startup. Sources, first found wins: the configured folder
/// (<see cref="Application.Utils.Models.StoreApiOptions.RulesPath"/>, which must exist when set), the <c>rules</c> folder
/// copied next to the application, the copy embedded in this assembly. A catalog that does not validate stops the
/// application: readiness and timelines must never run on half a catalog.
/// </summary>
/// <param name="catalog"></param>
public sealed partial class RulesCatalog(RuleCatalog catalog) : IRulesCatalog
{
    /// <summary>
    /// Major catalog version this build reads; another major version is refused.
    /// </summary>
    public const int SupportedMajorVersion = 1;

    /// <summary>
    /// Name of the manifest file.
    /// </summary>
    public const string ManifestFileName = "catalog.json";

    /// <summary>
    /// Folder name of the catalog next to the application.
    /// </summary>
    public const string FolderName = "rules";

    private const string _embeddedPrefix = "rules/";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly RuleCatalog _catalog = catalog;

    /// <inheritdoc/>
    public RuleCatalog Current => _catalog;

    /// <summary>
    /// Loads the catalog from the first available source.
    /// </summary>
    /// <param name="configuredPath"></param>
    /// <returns></returns>
    public static RulesCatalog Load(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!File.Exists(Path.Combine(configuredPath, ManifestFileName)))
                throw new InvalidOperationException($"The rules catalog folder '{configuredPath}' (Storeasy:Stores:RulesPath) has no {ManifestFileName}.");

            return new RulesCatalog(LoadFromFolder(configuredPath));
        }

        var nextToApp = Path.Combine(AppContext.BaseDirectory, FolderName);

        if (File.Exists(Path.Combine(nextToApp, ManifestFileName)))
            return new RulesCatalog(LoadFromFolder(nextToApp));

        return new RulesCatalog(LoadEmbedded());
    }

    /// <summary>
    /// Loads and validates the catalog in <paramref name="folder"/>.
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    public static RuleCatalog LoadFromFolder(string folder)
        => Build(File.ReadAllText(Path.Combine(folder, ManifestFileName)), path => File.ReadAllText(Path.Combine(folder, path)), folder);

    /// <summary>
    /// Loads and validates the copy embedded in this assembly.
    /// </summary>
    /// <returns></returns>
    public static RuleCatalog LoadEmbedded()
    {
        var assembly = typeof(RulesCatalog).Assembly;

        string Read(string name)
        {
            using var stream = assembly.GetManifestResourceStream(_embeddedPrefix + name)
                               ?? throw new InvalidOperationException($"The embedded rules catalog has no {name}.");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

        return Build(Read(ManifestFileName), Read, "embedded");
    }

    /// <summary>
    /// Parses and validates a manifest and its store files.
    /// </summary>
    /// <param name="manifestJson"></param>
    /// <param name="readFile"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public static RuleCatalog Build(string manifestJson, Func<string, string> readFile, string source)
    {
        var errors = new List<string>();
        var manifest = JsonSerializer.Deserialize<CatalogManifest>(manifestJson, _json);

        if (manifest?.Kind != "manifest")
            throw new InvalidOperationException("The rules catalog manifest is not a manifest.");

        if (manifest.CatalogVersion == null || !VersionPattern().IsMatch(manifest.CatalogVersion))
            throw new InvalidOperationException($"The rules catalog version '{manifest.CatalogVersion}' is not MAJOR.MINOR.PATCH.");

        if (int.Parse(manifest.CatalogVersion.Split('.')[0], System.Globalization.CultureInfo.InvariantCulture) != SupportedMajorVersion)
            throw new InvalidOperationException($"The rules catalog version {manifest.CatalogVersion} is not supported; this build reads {SupportedMajorVersion}.x.");

        var rules = new List<CatalogRule>();

        foreach (var file in manifest.Files ?? [])
        {
            var storeFile = JsonSerializer.Deserialize<StoreRulesFile>(readFile(file.Path), _json);

            if (storeFile?.Kind != "store-rules")
            {
                errors.Add($"{file.Path}: not a store rules file.");
                continue;
            }

            if (storeFile.CatalogVersion != manifest.CatalogVersion)
                errors.Add($"{file.Path}: version {storeFile.CatalogVersion} differs from the manifest's {manifest.CatalogVersion}.");

            if (storeFile.Store != file.Store || RuleStores.Parse(storeFile.Store) == null)
                errors.Add($"{file.Path}: store '{storeFile.Store}' does not match the manifest's '{file.Store}'.");

            foreach (var rule in storeFile.Rules ?? [])
            {
                if (rule.Store != storeFile.Store)
                    errors.Add($"{rule.Id}: store '{rule.Store}' in the {storeFile.Store} file.");

                if (rule.Requirement?.Level == RuleRequirement.RequiredFrom && rule.Requirement.From == null)
                    errors.Add($"{rule.Id}: required-from without a date.");

                rules.Add(rule);
            }
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id) || !ids.Add(rule.Id))
                errors.Add($"{rule.Id}: empty or duplicate id.");
        }

        foreach (var rule in rules)
        {
            foreach (var reference in (rule.Requirement?.FallbackTo ?? []).Concat(rule.Requirement?.SatisfiedBy ?? []).Where(r => !ids.Contains(r)))
                errors.Add($"{rule.Id}: refers to unknown rule {reference}.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException($"The rules catalog {manifest.CatalogVersion} ({source}) is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");

        return new RuleCatalog(manifest.CatalogVersion, manifest.GeneratedAt, rules, source);
    }

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")]
    private static partial Regex VersionPattern();
}
