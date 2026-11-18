using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Studio;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// <see cref="IScreenshotSizeSource"/> over the rules catalog files (<c>rules/catalog.json</c> and the store files it
/// lists). Reads only the rules of <see cref="StudioClassMap"/>; sizes, counts, requirement levels and dates come from the
/// files; the export size of a class is its largest listed size. Loaded once, on first use, so a host without the catalog starts and fails only the studio calls. The folder is
/// the configured one, a <c>rules</c> folder next to the application, or the nearest <c>rules</c> folder above it (a source
/// checkout, tests).
/// </summary>
/// <param name="configuredFolder">Configured catalog folder (<c>Storeasy:Studio:RulesPath</c>); empty to search.</param>
public sealed class FileScreenshotSizeSource(string configuredFolder) : IScreenshotSizeSource
{
    /// <summary>
    /// Name of the catalog manifest.
    /// </summary>
    public const string ManifestFileName = "catalog.json";

    private const string _folderName = "rules";

    private static readonly string[] _levelOrder = ["optional", "recommended", "required-from", "required"];

    private readonly Lazy<ScreenshotSizeCatalog> _catalog = new(() => Load(ResolveFolder(configuredFolder)));

    /// <inheritdoc/>
    public ScreenshotSizeCatalog Current => _catalog.Value;

    /// <summary>
    /// The catalog folder: <paramref name="configured"/> when set, else <c>rules</c> next to the application, else the
    /// nearest <c>rules</c> folder with a manifest above the application.
    /// </summary>
    /// <param name="configured"></param>
    /// <returns></returns>
    public static string ResolveFolder(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, _folderName);

            if (File.Exists(Path.Combine(candidate, ManifestFileName)))
                return candidate;
        }

        throw new DirectoryNotFoundException($"The store rules catalog ({_folderName}/{ManifestFileName}) was not found next to or above {AppContext.BaseDirectory}; set Storeasy:Studio:RulesPath.");
    }

    /// <summary>
    /// Reads the studio classes from the catalog in <paramref name="folder"/>. Throws when a studio rule is missing or a
    /// preset size is outside its rule's range.
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    public static ScreenshotSizeCatalog Load(string folder)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, ManifestFileName)));

        var version = manifest.RootElement.GetProperty("catalogVersion").GetString();
        var documents = manifest.RootElement.GetProperty("files").EnumerateArray()
                                .Select(file => JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, file.GetProperty("path").GetString()))))
                                .ToList();

        try
        {
            var rules = documents.SelectMany(d => d.RootElement.GetProperty("rules").EnumerateArray())
                                 .ToDictionary(r => r.GetProperty("id").GetString(), r => r, StringComparer.Ordinal);

            var classes = StudioClassMap.Definitions.Select(definition => Build(definition, rules)).ToList();

            return new ScreenshotSizeCatalog(version, classes);
        }
        finally
        {
            foreach (var document in documents)
                document.Dispose();
        }
    }

    private static ScreenshotClass Build(StudioClassDefinition definition, Dictionary<string, JsonElement> rules)
    {
        if (!rules.TryGetValue(definition.RuleId, out var rule))
            throw new InvalidOperationException($"The rules catalog has no rule '{definition.RuleId}' the studio exports to.");

        var parameters = rule.TryGetProperty("params", out var p) ? p : default;
        var requirement = rule.GetProperty("requirement");
        var general = definition.GeneralRuleId != null && rules.TryGetValue(definition.GeneralRuleId, out var g) ? g : (JsonElement?)null;
        var generalParams = general?.TryGetProperty("params", out var gp) == true ? gp : default;

        var sizes = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("sizes", out var s)
            ? s.EnumerateArray().Select(size => new PixelSize(size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32())).ToList()
            : [];

        var level = requirement.GetProperty("level").GetString();

        if (general != null)
        {
            var generalLevel = general.Value.GetProperty("requirement").GetProperty("level").GetString();

            if (Array.IndexOf(_levelOrder, generalLevel) > Array.IndexOf(_levelOrder, level))
                level = generalLevel;
        }

        var screenshotClass = new ScreenshotClass
        {
            Key = definition.RuleId,
            Store = StoreOf(rule.GetProperty("store").GetString()),
            LabelEn = rule.GetProperty("title").GetProperty("en").GetString(),
            LabelTr = rule.GetProperty("title").TryGetProperty("tr", out var tr) ? tr.GetString() : null,
            Frame = definition.Frame,
            Family = definition.Family,
            ExportSize = definition.PresetSize ?? LargestSize(sizes, definition.RuleId),
            AcceptedSizes = sizes,
            MinDimension = Int(parameters, "minDimension") ?? Math.Min(Int(parameters, "minWidth") ?? 0, Int(parameters, "minHeight") ?? 0),
            MaxDimension = Int(parameters, "maxDimension") ?? Math.Max(Int(parameters, "maxWidth") ?? 0, Int(parameters, "maxHeight") ?? 0),
            Level = level,
            RequiredFrom = requirement.TryGetProperty("from", out var from) && from.ValueKind == JsonValueKind.String ? DateOnly.ParseExact(from.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
            SatisfiedBy = requirement.TryGetProperty("satisfiedBy", out var satisfied) ? [.. satisfied.EnumerateArray().Select(x => x.GetString())] : [],
            MinCount = Int(parameters, "minCount") ?? Int(generalParams, "minCount") ?? 1,
            MaxCount = Int(parameters, "maxCount") ?? Int(generalParams, "maxCount") ?? (definition.SingleImage ? 1 : 10),
            SingleImage = definition.SingleImage,
            Note = requirement.TryGetProperty("condition", out var condition) ? condition.GetString() : null,
        };

        if (definition.PresetSize != null)
            CheckPreset(screenshotClass, parameters);

        return screenshotClass;
    }

    /// <summary>
    /// A preset must sit inside its rule's range and keep a required aspect ratio, so a catalog change that moves the
    /// range is noticed at load instead of producing images the store refuses.
    /// </summary>
    private static void CheckPreset(ScreenshotClass screenshotClass, JsonElement parameters)
    {
        var size = screenshotClass.ExportSize;

        if (!MockupExportRules.Accepts(screenshotClass, size))
            throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' is outside the catalog's range.");

        if (Int(parameters, "minWidth") is int minWidth && Int(parameters, "minHeight") is int minHeight
            && (Math.Max(size.Width, size.Height) < Math.Max(minWidth, minHeight) || Math.Min(size.Width, size.Height) < Math.Min(minWidth, minHeight)))
            throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' is below the catalog's minimum {minWidth}x{minHeight}.");

        if (parameters.TryGetProperty("aspectRatio", out var aspect) && aspect.GetString()?.Split(':') is [var a, var b])
        {
            var ratio = float.Parse(a, CultureInfo.InvariantCulture) / float.Parse(b, CultureInfo.InvariantCulture);
            var actual = Math.Max(size.Width, size.Height) / (float)Math.Min(size.Width, size.Height);

            if (Math.Abs(actual - Math.Max(ratio, 1 / ratio)) > 0.01f)
                throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' does not keep the catalog's aspect ratio {aspect.GetString()}.");
        }

        if (parameters.TryGetProperty("maxAspectRatio", out var maxAspect)
            && Math.Max(size.Width, size.Height) / (float)Math.Min(size.Width, size.Height) > maxAspect.GetSingle())
            throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' exceeds the catalog's aspect ratio limit.");
    }

    /// <summary>
    /// The largest listed size in the orientation of the first one (the store scales it down for smaller devices of the
    /// class; it never scales up).
    /// </summary>
    private static PixelSize LargestSize(List<PixelSize> sizes, string ruleId)
    {
        if (sizes.Count == 0)
            throw new InvalidOperationException($"Rule '{ruleId}' lists no sizes and the studio has no preset for it.");

        return sizes.Where(s => s.IsLandscape == sizes[0].IsLandscape).MaxBy(s => (long)s.Width * s.Height);
    }

    private static int? Int(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    private static StoreKind StoreOf(string store) => store switch
    {
        "apple" => StoreKind.Apple,
        "google" => StoreKind.Google,
        "microsoft" => StoreKind.Microsoft,
        _ => throw new InvalidOperationException($"Unknown store '{store}' in the rules catalog."),
    };
}
