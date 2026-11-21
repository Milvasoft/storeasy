namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One variant of a store listing experiment: an App Store treatment, the page of a custom product page, or a Play
/// experiment variant. Only what the variant changes is set; the rest stays as the store's current page.
/// </summary>
public class AsoExperimentVariant
{
    /// <summary>
    /// Short key inside the experiment ("A", "B", "C").
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Name shown in the store console.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// App Store treatments: alternate icon name from the build (asset catalog), null when the icon does not change.
    /// </summary>
    public string AppIconName { get; set; }

    /// <summary>
    /// The store's id (App Store treatment id), once written.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// What changes per locale.
    /// </summary>
    public List<AsoVariantLocale> Locales { get; set; } = [];
}

/// <summary>
/// What a variant changes in one locale.
/// </summary>
public class AsoVariantLocale
{
    /// <summary>
    /// Locale (store code, e.g. en-US).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// App Store custom product page promotional text.
    /// </summary>
    public string PromotionalText { get; set; }

    /// <summary>
    /// Play short description.
    /// </summary>
    public string ShortDescription { get; set; }

    /// <summary>
    /// Play full description.
    /// </summary>
    public string FullDescription { get; set; }

    /// <summary>
    /// Screenshot and preview sets (or Play graphics) of the variant, from uploaded assets or mockup exports.
    /// </summary>
    public List<AsoVariantMedia> Media { get; set; } = [];
}

/// <summary>
/// One media set of a variant.
/// </summary>
public class AsoVariantMedia
{
    /// <summary>
    /// Screenshot or preview.
    /// </summary>
    public Enums.MediaKind Kind { get; set; }

    /// <summary>
    /// Store class key (App Store display type, Play image type).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Assets in order.
    /// </summary>
    public List<long> AssetIds { get; set; } = [];
}
