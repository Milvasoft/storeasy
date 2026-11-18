using Storeasy.Application.Interfaces;
using Storeasy.Domain.Rules;
using Storeasy.Domain.Studio;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// <see cref="IScreenshotSizeSource"/> over the store rules catalog the API loads at startup (<see cref="IRulesCatalog"/>,
/// one catalog and one <c>Storeasy:Stores:RulesPath</c> setting for the whole API). Reads only the rules of
/// <see cref="StudioClassMap"/>; sizes, counts, requirement levels and dates come from the catalog, and the export size of
/// a class is its largest listed size. Built once, on first use.
/// </summary>
/// <param name="rulesCatalog"></param>
public sealed class RulesCatalogScreenshotSizeSource(IRulesCatalog rulesCatalog) : IScreenshotSizeSource
{
    private static readonly string[] _levelOrder = [RuleRequirement.Optional, RuleRequirement.Recommended, RuleRequirement.RequiredFrom, RuleRequirement.Required];

    private readonly Lazy<ScreenshotSizeCatalog> _catalog = new(() => Build(rulesCatalog.Current));

    /// <inheritdoc/>
    public ScreenshotSizeCatalog Current => _catalog.Value;

    /// <summary>
    /// Reads the studio classes from <paramref name="catalog"/>. Throws when a studio rule is missing or a preset size is
    /// outside its rule's range.
    /// </summary>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static ScreenshotSizeCatalog Build(RuleCatalog catalog)
        => new(catalog.Version, [.. StudioClassMap.Definitions.Select(definition => Build(definition, catalog))]);

    private static ScreenshotClass Build(StudioClassDefinition definition, RuleCatalog catalog)
    {
        var rule = catalog.Find(definition.RuleId) ?? throw new InvalidOperationException($"The rules catalog has no rule '{definition.RuleId}' the studio exports to.");
        var general = definition.GeneralRuleId == null ? null : catalog.Find(definition.GeneralRuleId);
        var parameters = rule.Params ?? new RuleParams();
        var sizes = (parameters.Sizes ?? []).Select(s => new PixelSize(s.Width, s.Height)).ToList();
        var level = rule.Requirement?.Level ?? RuleRequirement.Optional;
        var generalLevel = general?.Requirement?.Level;

        if (generalLevel != null && Array.IndexOf(_levelOrder, generalLevel) > Array.IndexOf(_levelOrder, level))
            level = generalLevel;

        var screenshotClass = new ScreenshotClass
        {
            Key = definition.RuleId,
            Store = rule.StoreKind ?? throw new InvalidOperationException($"Rule '{rule.Id}' has an unknown store."),
            LabelEn = rule.Title?.En,
            LabelTr = rule.Title?.Tr,
            Frame = definition.Frame,
            Family = definition.Family,
            ExportSize = definition.PresetSize ?? LargestSize(sizes, definition.RuleId),
            AcceptedSizes = sizes,
            MinDimension = parameters.MinDimension ?? Math.Min(Extra(parameters, "minWidth") ?? 0, Extra(parameters, "minHeight") ?? 0),
            MaxDimension = parameters.MaxDimension ?? Math.Max(Extra(parameters, "maxWidth") ?? 0, Extra(parameters, "maxHeight") ?? 0),
            Level = level,
            RequiredFrom = rule.Requirement?.From,
            SatisfiedBy = rule.Requirement?.SatisfiedBy ?? [],
            MinCount = parameters.MinCount ?? general?.Params?.MinCount ?? 1,
            MaxCount = parameters.MaxCount ?? general?.Params?.MaxCount ?? (definition.SingleImage ? 1 : 10),
            SingleImage = definition.SingleImage,
            Note = rule.Requirement?.Condition,
        };

        if (definition.PresetSize != null)
            CheckPreset(screenshotClass, parameters);

        return screenshotClass;
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

    /// <summary>
    /// A preset must sit inside its rule's range and keep a required aspect ratio, so a catalog change that moves the
    /// range is noticed at load instead of producing images the store refuses.
    /// </summary>
    private static void CheckPreset(ScreenshotClass screenshotClass, RuleParams parameters)
    {
        var size = screenshotClass.ExportSize;
        var longSide = Math.Max(size.Width, size.Height);
        var shortSide = Math.Min(size.Width, size.Height);

        if (!MockupExportRules.Accepts(screenshotClass, size))
            throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' is outside the catalog's range.");

        if (Extra(parameters, "minWidth") is int minWidth && Extra(parameters, "minHeight") is int minHeight
            && (longSide < Math.Max(minWidth, minHeight) || shortSide < Math.Min(minWidth, minHeight)))
            throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' is below the catalog's minimum {minWidth}x{minHeight}.");

        if (parameters.Extra != null && parameters.Extra.TryGetValue("aspectRatio", out var aspect) && aspect.GetString()?.Split(':') is [var a, var b])
        {
            var ratio = float.Parse(a, CultureInfo.InvariantCulture) / float.Parse(b, CultureInfo.InvariantCulture);

            if (Math.Abs((longSide / (float)shortSide) - Math.Max(ratio, 1 / ratio)) > 0.01f)
                throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' does not keep the catalog's aspect ratio {aspect.GetString()}.");
        }

        if (parameters.MaxAspectRatio is double maxAspect && longSide / (double)shortSide > maxAspect)
            throw new InvalidOperationException($"The studio preset {size} of '{screenshotClass.Key}' exceeds the catalog's aspect ratio limit.");
    }

    private static int? Extra(RuleParams parameters, string name)
        => parameters.Extra != null && parameters.Extra.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
}
