using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Studio;

/// <summary>
/// Which rules of the store rules catalog the studio exports to, and how it draws them. Sizes, counts and requirement
/// levels are never kept here: they are read from the catalog. Only rules that give a range instead of sizes (Play phone
/// and tablets, Microsoft desktop) carry a preset, and the preset is checked against the catalog's range when the catalog
/// loads. Order is the export order: required classes first, then by store.
/// </summary>
public static class StudioClassMap
{
    /// <summary>
    /// The class the editor opens with.
    /// </summary>
    public const string DefaultClassKey = "apple.screenshots.iphone-dynamic-island-medium";

    /// <summary>
    /// Studio classes in export order.
    /// </summary>
    public static IReadOnlyList<StudioClassDefinition> Definitions { get; } =
    [
        new("apple.screenshots.iphone-dynamic-island-medium", MockupFrameKind.Phone, MockupDeviceFamily.Phone),
        new("apple.screenshots.iphone-dynamic-island-large", MockupFrameKind.Phone, MockupDeviceFamily.Phone),
        new("apple.screenshots.iphone-face-id-large", MockupFrameKind.Phone, MockupDeviceFamily.Phone),
        new("apple.screenshots.ipad-13", MockupFrameKind.Tablet, MockupDeviceFamily.Tablet),
        new("apple.screenshots.iphone-duo-outer", MockupFrameKind.FoldableOuter, MockupDeviceFamily.Phone),
        new("apple.screenshots.iphone-duo-inner", MockupFrameKind.FoldableInner, MockupDeviceFamily.Phone),
        new("google.screenshots.phone", MockupFrameKind.Phone, MockupDeviceFamily.Phone, new PixelSize(1080, 1920), GeneralRuleId: "google.screenshots.general"),
        new("google.screenshots.tablet-7", MockupFrameKind.Tablet, MockupDeviceFamily.Tablet, new PixelSize(1080, 1920)),
        new("google.screenshots.tablet-10", MockupFrameKind.Tablet, MockupDeviceFamily.Tablet, new PixelSize(1440, 2560)),
        new("google.graphics.feature-graphic", MockupFrameKind.None, MockupDeviceFamily.Phone, SingleImage: true),
        new("microsoft.screenshots.desktop", MockupFrameKind.Desktop, MockupDeviceFamily.Desktop, new PixelSize(1920, 1080), GeneralRuleId: "microsoft.screenshots.general"),
    ];

    /// <summary>
    /// Whether <paramref name="classKey"/> is a studio class.
    /// </summary>
    /// <param name="classKey"></param>
    /// <returns></returns>
    public static bool IsKnown(string classKey) => Definitions.Any(d => d.RuleId == classKey);
}

/// <summary>
/// How the studio uses one catalog rule.
/// </summary>
/// <param name="RuleId">Rule id in the catalog; also the class key.</param>
/// <param name="Frame">Frame the images are drawn with.</param>
/// <param name="Family">Raw screenshot family the class prefers.</param>
/// <param name="PresetSize">Export size for a rule that gives a range instead of sizes; null to use the first catalog size.</param>
/// <param name="GeneralRuleId">The store's general screenshot rule whose level applies to this main class.</param>
/// <param name="SingleImage">One image per locale (feature graphic).</param>
public sealed record StudioClassDefinition(string RuleId,
                                           MockupFrameKind Frame,
                                           MockupDeviceFamily Family,
                                           PixelSize? PresetSize = null,
                                           string GeneralRuleId = null,
                                           bool SingleImage = false);
