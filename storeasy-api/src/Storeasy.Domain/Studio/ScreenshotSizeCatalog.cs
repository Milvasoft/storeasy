using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Studio;

/// <summary>
/// The screenshot classes the studio exports to, read from the store rules catalog (rules/*.json): sizes, counts and how
/// much each store wants them. Built once by the size source; immutable.
/// </summary>
/// <param name="catalogVersion">Version of the rules catalog the classes were read from.</param>
/// <param name="classes">Classes in export order (required first within a store).</param>
public sealed class ScreenshotSizeCatalog(string catalogVersion, IReadOnlyList<ScreenshotClass> classes)
{
    private readonly Dictionary<string, ScreenshotClass> _byKey = classes.ToDictionary(c => c.Key, StringComparer.Ordinal);

    /// <summary>
    /// Version of the rules catalog.
    /// </summary>
    public string CatalogVersion { get; } = catalogVersion;

    /// <summary>
    /// Classes in export order.
    /// </summary>
    public IReadOnlyList<ScreenshotClass> Classes { get; } = classes;

    /// <summary>
    /// The class with <paramref name="key"/>, or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public ScreenshotClass Find(string key) => key != null && _byKey.TryGetValue(key, out var found) ? found : null;
}

/// <summary>
/// One screenshot class of a store (a display type in App Store Connect, a device type in Play, a device family in the
/// Microsoft Store), as the studio uses it.
/// </summary>
public sealed class ScreenshotClass
{
    /// <summary>
    /// Class key: the id of its rule in the catalog (e.g. <c>apple.screenshots.ipad-13</c>).
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Store of the class.
    /// </summary>
    public StoreKind Store { get; init; }

    /// <summary>
    /// English label (catalog title).
    /// </summary>
    public string LabelEn { get; init; }

    /// <summary>
    /// Turkish label (catalog title).
    /// </summary>
    public string LabelTr { get; init; }

    /// <summary>
    /// Frame its images are drawn with.
    /// </summary>
    public MockupFrameKind Frame { get; init; }

    /// <summary>
    /// Raw screenshot family it prefers; others fall back to the phone screenshot.
    /// </summary>
    public MockupDeviceFamily Family { get; init; }

    /// <summary>
    /// Pixel size the export renders (portrait for devices, landscape for desktop and the feature graphic).
    /// </summary>
    public PixelSize ExportSize { get; init; }

    /// <summary>
    /// Every size the store accepts for the class (both orientations). Empty when the store gives ranges instead.
    /// </summary>
    public IReadOnlyList<PixelSize> AcceptedSizes { get; init; } = [];

    /// <summary>
    /// Smallest accepted side for range based classes (Play, Microsoft); 0 when the store lists exact sizes.
    /// </summary>
    public int MinDimension { get; init; }

    /// <summary>
    /// Largest accepted side for range based classes; 0 when the store lists exact sizes.
    /// </summary>
    public int MaxDimension { get; init; }

    /// <summary>
    /// Catalog requirement level: <c>required</c>, <c>required-from</c>, <c>recommended</c> or <c>optional</c>. For a class
    /// that is the store's main class it is the level of the store's general screenshot rule when that is stricter.
    /// </summary>
    public string Level { get; init; }

    /// <summary>
    /// Date the class becomes required (for <c>required-from</c>).
    /// </summary>
    public DateOnly? RequiredFrom { get; init; }

    /// <summary>
    /// Classes that meet this class's requirement when they are provided.
    /// </summary>
    public IReadOnlyList<string> SatisfiedBy { get; init; } = [];

    /// <summary>
    /// Fewest images per locale the store expects.
    /// </summary>
    public int MinCount { get; init; }

    /// <summary>
    /// Most images per locale the store accepts.
    /// </summary>
    public int MaxCount { get; init; }

    /// <summary>
    /// The class takes one image per locale (a feature graphic), drawn from the first slot's caption without a device.
    /// </summary>
    public bool SingleImage { get; init; }

    /// <summary>
    /// Short note of the catalog (condition of the requirement).
    /// </summary>
    public string Note { get; init; }
}

/// <summary>
/// A pixel size.
/// </summary>
public readonly record struct PixelSize
{
    /// <summary>
    /// Creates a size.
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public PixelSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Width divided by height.
    /// </summary>
    public float Aspect => Height == 0 ? 0 : Width / (float)Height;

    /// <summary>
    /// Whether the size is landscape.
    /// </summary>
    public bool IsLandscape => Width > Height;

    /// <inheritdoc/>
    public override string ToString() => $"{Width}x{Height}";
}
