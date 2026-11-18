using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;

namespace Storeasy.Domain.Studio;

/// <summary>
/// Pure rules of the studio export: requirement of a class for an app, images per locale, accepted sizes, the frame a
/// class is drawn with, which raw screenshot and caption a slot uses, and the names of the produced files.
/// </summary>
public static class MockupExportRules
{
    /// <summary>
    /// Largest upscale of a raw screenshot that is still drawn; above it the image fails with
    /// <see cref="MockupWarningCodes.AssetTooSmall"/> (the result would be blurry).
    /// </summary>
    public const float MaxUpscale = 1.25f;

    /// <summary>
    /// Upscale above which a warning is written.
    /// </summary>
    public const float WarnUpscale = 1.0f;

    /// <summary>
    /// Requirement of <paramref name="screenshotClass"/> on <paramref name="today"/>, given the classes the export provides
    /// (<paramref name="providedKeys"/>): a required class that another provided class satisfies is optional; a class
    /// required from a later date is recommended until then.
    /// </summary>
    /// <param name="screenshotClass"></param>
    /// <param name="today"></param>
    /// <param name="providedKeys"></param>
    /// <returns></returns>
    public static MockupRequirement Requirement(ScreenshotClass screenshotClass, DateOnly today, IReadOnlyCollection<string> providedKeys)
    {
        switch (screenshotClass.Level)
        {
            case "required":
                return screenshotClass.SatisfiedBy.Any(providedKeys.Contains) ? MockupRequirement.Optional : MockupRequirement.Required;
            case "required-from":
                return screenshotClass.RequiredFrom is DateOnly from && from <= today ? MockupRequirement.Required : MockupRequirement.Recommended;
            case "recommended":
                return MockupRequirement.Recommended;
            default:
                return MockupRequirement.Optional;
        }
    }

    /// <summary>
    /// Classes an export provides by default from <paramref name="classes"/>: every class the catalog does not call
    /// optional, plus the optional classes that stand in for a required one (the 6.9" iPhone class satisfies the Face ID
    /// large class, so storeasy renders the newer class instead).
    /// </summary>
    /// <param name="classes"></param>
    /// <returns></returns>
    public static List<string> DefaultProvided(IReadOnlyCollection<ScreenshotClass> classes)
    {
        var keys = classes.Select(c => c.Key).ToHashSet();
        var satisfiers = classes.Where(c => c.Level == "required").SelectMany(c => c.SatisfiedBy).Where(keys.Contains);

        return [.. classes.Where(c => c.Level != "optional").Select(c => c.Key).Concat(satisfiers).Distinct()];
    }

    /// <summary>
    /// <see cref="Requirement"/> for a plan: an optional class the default selection provides in place of a required one
    /// is recommended.
    /// </summary>
    /// <param name="screenshotClass"></param>
    /// <param name="today"></param>
    /// <param name="providedKeys"></param>
    /// <param name="defaultProvided"></param>
    /// <returns></returns>
    public static MockupRequirement PlanRequirement(ScreenshotClass screenshotClass, DateOnly today, IReadOnlyCollection<string> providedKeys, IReadOnlyCollection<string> defaultProvided)
    {
        var requirement = Requirement(screenshotClass, today, providedKeys);

        return requirement == MockupRequirement.Optional && screenshotClass.Level == "optional" && defaultProvided.Contains(screenshotClass.Key)
            ? MockupRequirement.Recommended
            : requirement;
    }

    /// <summary>
    /// Images one locale gets in <paramref name="screenshotClass"/> from a template with <paramref name="slotCount"/> slots.
    /// </summary>
    /// <param name="screenshotClass"></param>
    /// <param name="slotCount"></param>
    /// <returns></returns>
    public static int ImagesPerLocale(ScreenshotClass screenshotClass, int slotCount)
    {
        if (screenshotClass.SingleImage)
            return Math.Min(1, slotCount);

        return screenshotClass.MaxCount > 0 ? Math.Min(slotCount, screenshotClass.MaxCount) : slotCount;
    }

    /// <summary>
    /// Whether the store accepts <paramref name="size"/> for <paramref name="screenshotClass"/>: one of its listed sizes, or
    /// inside its range.
    /// </summary>
    /// <param name="screenshotClass"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public static bool Accepts(ScreenshotClass screenshotClass, PixelSize size)
    {
        if (screenshotClass.AcceptedSizes.Count > 0)
            return screenshotClass.AcceptedSizes.Contains(size);

        var shortSide = Math.Min(size.Width, size.Height);
        var longSide = Math.Max(size.Width, size.Height);

        return shortSide >= screenshotClass.MinDimension && (screenshotClass.MaxDimension == 0 || longSide <= screenshotClass.MaxDimension);
    }

    /// <summary>
    /// Frame a class is drawn with under the template's <see cref="MockupFrameSpec.Kind"/>. A class without a device (the
    /// feature graphic) never gets one.
    /// </summary>
    /// <param name="screenshotClass"></param>
    /// <param name="frameKind"></param>
    /// <returns></returns>
    public static MockupFrameKind ResolveFrame(ScreenshotClass screenshotClass, string frameKind)
    {
        if (screenshotClass.Frame == MockupFrameKind.None)
            return MockupFrameKind.None;

        return frameKind switch
        {
            MockupSpecRules.FrameKinds.None => MockupFrameKind.None,
            MockupSpecRules.FrameKinds.Phone => MockupFrameKind.Phone,
            MockupSpecRules.FrameKinds.Tablet => MockupFrameKind.Tablet,
            MockupSpecRules.FrameKinds.Desktop => MockupFrameKind.Desktop,
            MockupSpecRules.FrameKinds.Foldable => screenshotClass.Frame == MockupFrameKind.FoldableInner ? MockupFrameKind.FoldableInner : MockupFrameKind.FoldableOuter,
            _ => screenshotClass.Frame,
        };
    }

    /// <summary>
    /// The raw screenshot of slot <paramref name="order"/> for <paramref name="family"/> and <paramref name="locale"/>:
    /// the locale's own image of the family, the family's shared image, then the same two for the phone. Returns null when
    /// the slot has no image; <c>FellBack</c> tells that a phone image stands in for another family.
    /// </summary>
    /// <param name="slots"></param>
    /// <param name="order"></param>
    /// <param name="family"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public static (MockupSlot Slot, bool FellBack) PickSlotAsset(IEnumerable<MockupSlot> slots, int order, MockupDeviceFamily family, string locale)
    {
        var candidates = slots.Where(s => s.Order == order).ToList();

        MockupSlot Find(MockupDeviceFamily f) => candidates.FirstOrDefault(s => s.Family == f && string.Equals(s.Locale, locale, StringComparison.OrdinalIgnoreCase))
                                                 ?? candidates.FirstOrDefault(s => s.Family == f && s.Locale == null);

        var own = Find(family);

        if (own != null || family == MockupDeviceFamily.Phone)
            return (own, false);

        var phone = Find(MockupDeviceFamily.Phone);

        return (phone, phone != null);
    }

    /// <summary>
    /// Title and subtitle of slot <paramref name="slotOrder"/> in <paramref name="locale"/>; when the locale has no title
    /// for the slot, the primary locale's caption is used and <c>FellBack</c> is true.
    /// </summary>
    /// <param name="captions"></param>
    /// <param name="locale"></param>
    /// <param name="primaryLocale"></param>
    /// <param name="slotOrder"></param>
    /// <returns></returns>
    public static (string Title, string Subtitle, bool FellBack) PickCaption(IEnumerable<MockupCaption> captions, string locale, string primaryLocale, int slotOrder)
    {
        var list = captions.Where(c => c.SlotOrder == slotOrder).ToList();
        var own = list.FirstOrDefault(c => string.Equals(c.Locale, locale, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(own?.Title))
            return (own.Title, own.Subtitle, false);

        var primary = list.FirstOrDefault(c => string.Equals(c.Locale, primaryLocale, StringComparison.OrdinalIgnoreCase));

        if (primary == null || string.Equals(locale, primaryLocale, StringComparison.OrdinalIgnoreCase))
            return (own?.Title, own?.Subtitle, false);

        return (primary.Title, primary.Subtitle, true);
    }

    /// <summary>
    /// Path of an image inside the export zip: <c>{store}/{class}/{locale}/{slot:00}.png</c>.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="classKey"></param>
    /// <param name="locale"></param>
    /// <param name="slotOrder"></param>
    /// <returns></returns>
    public static string ZipEntryPath(StoreKind store, string classKey, string locale, int slotOrder)
        => $"{store.ToString().ToLowerInvariant()}/{ShortClassName(classKey)}/{locale}/{slotOrder:00}.png";

    /// <summary>
    /// File name of a produced image: <c>{class}-{locale}-{slot:00}.png</c>.
    /// </summary>
    /// <param name="classKey"></param>
    /// <param name="locale"></param>
    /// <param name="slotOrder"></param>
    /// <returns></returns>
    public static string OutputFileName(string classKey, string locale, int slotOrder) => $"{ShortClassName(classKey)}-{locale}-{slotOrder:00}.png";

    /// <summary>
    /// The class key without its store and category (<c>apple.screenshots.ipad-13</c> is <c>ipad-13</c>).
    /// </summary>
    /// <param name="classKey"></param>
    /// <returns></returns>
    public static string ShortClassName(string classKey) => classKey?.Split('.') is { Length: >= 3 } parts ? string.Join('.', parts[2..]) : classKey;
}

/// <summary>
/// Codes of render warnings and failures (documented in storeasy-api/docs/studio.md).
/// </summary>
public static class MockupWarningCodes
{
    /// <summary>A caption needs more lines than its style allows, even at the smallest auto fit size.</summary>
    public const string CaptionOverflow = "CaptionOverflow";

    /// <summary>The locale has no caption for the slot; the primary locale's caption was drawn.</summary>
    public const string CaptionFallback = "CaptionFallback";

    /// <summary>The requested font weight is not embedded; the nearest weight was drawn.</summary>
    public const string FontMissing = "FontMissing";

    /// <summary>The raw screenshot had transparent pixels; they were flattened onto the screen color.</summary>
    public const string AlphaFlattened = "AlphaFlattened";

    /// <summary>The raw screenshot was enlarged up to <see cref="MockupExportRules.MaxUpscale"/>.</summary>
    public const string AssetUpscaled = "AssetUpscaled";

    /// <summary>The raw screenshot is too small for the class (failure).</summary>
    public const string AssetTooSmall = "AssetTooSmall";

    /// <summary>The slot has no raw screenshot; the screen is drawn empty.</summary>
    public const string ScreenshotMissing = "ScreenshotMissing";

    /// <summary>A phone screenshot stands in for the class's own family.</summary>
    public const string FamilyFallback = "FamilyFallback";

    /// <summary>The raw screenshot's aspect differs from the screen's; it is fitted inside instead of cropped.</summary>
    public const string AspectMismatch = "AspectMismatch";

    /// <summary>The background image is missing or unreadable; the first background color was used.</summary>
    public const string BackgroundMissing = "BackgroundMissing";

    /// <summary>The raw screenshot could not be decoded (failure).</summary>
    public const string AssetUnreadable = "AssetUnreadable";
}
