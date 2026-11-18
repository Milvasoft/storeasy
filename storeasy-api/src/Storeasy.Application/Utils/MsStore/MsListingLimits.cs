using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Microsoft Store listing limits read from the rule catalog by rule id (<c>rules/microsoft.json</c>). A rule missing from
/// the catalog falls back to the value of catalog v1. Title and add-on texts are not in the catalog and stay fixed.
/// </summary>
public sealed class MsListingLimits
{
    /// <summary>Rule id of the description limit.</summary>
    public const string DescriptionRule = "microsoft.listing-limits.description";

    /// <summary>Rule id of the short description limit.</summary>
    public const string ShortDescriptionRule = "microsoft.listing-limits.short-description";

    /// <summary>Rule id of the what's new limit.</summary>
    public const string WhatsNewRule = "microsoft.listing-limits.whats-new";

    /// <summary>Rule id of the product features limits.</summary>
    public const string FeaturesRule = "microsoft.listing-limits.product-features";

    /// <summary>Rule id of the search terms limits.</summary>
    public const string SearchTermsRule = "microsoft.listing-limits.search-terms";

    /// <summary>Rule id of the general screenshot rules (other device families, caption).</summary>
    public const string ScreenshotsRule = "microsoft.screenshots.general";

    /// <summary>Rule id of the desktop screenshot rules.</summary>
    public const string DesktopScreenshotsRule = "microsoft.screenshots.desktop";

    /// <summary>Title of an app listing (reserved name length in Partner Center; not in the catalog).</summary>
    public const int TitleLength = 256;

    /// <summary>Add-on listing title (not in the catalog).</summary>
    public const int AddonTitle = 100;

    /// <summary>Add-on listing description (not in the catalog).</summary>
    public const int AddonDescription = 200;

    /// <summary>
    /// The values of catalog v1.
    /// </summary>
    public static MsListingLimits Default { get; } = new();

    /// <summary>Description characters.</summary>
    public int Description { get; init; } = 10000;

    /// <summary>Short description characters.</summary>
    public int ShortDescription { get; init; } = 1000;

    /// <summary>What's new characters.</summary>
    public int WhatsNew { get; init; } = 1500;

    /// <summary>Product features.</summary>
    public int FeatureCount { get; init; } = 20;

    /// <summary>Characters of a product feature.</summary>
    public int FeatureLength { get; init; } = 200;

    /// <summary>Search terms.</summary>
    public int SearchTermCount { get; init; } = 7;

    /// <summary>Characters of a search term.</summary>
    public int SearchTermLength { get; init; } = 30;

    /// <summary>Desktop screenshots per language.</summary>
    public int DesktopScreenshots { get; init; } = 10;

    /// <summary>Screenshots of each other device family per language.</summary>
    public int OtherScreenshots { get; init; } = 8;

    /// <summary>Characters of an image caption.</summary>
    public int ImageCaption { get; init; } = 200;

    /// <summary>
    /// Limits from <paramref name="catalog"/>; missing rules keep the catalog v1 values.
    /// </summary>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static MsListingLimits From(RuleCatalog catalog)
    {
        RuleParams Params(string id) => catalog?.Find(id)?.Params;

        var screenshots = Params(ScreenshotsRule);

        return new MsListingLimits
        {
            Description = Params(DescriptionRule)?.MaxChars ?? Default.Description,
            ShortDescription = Params(ShortDescriptionRule)?.MaxChars ?? Default.ShortDescription,
            WhatsNew = Params(WhatsNewRule)?.MaxChars ?? Default.WhatsNew,
            FeatureCount = Params(FeaturesRule)?.MaxItems ?? Default.FeatureCount,
            FeatureLength = Params(FeaturesRule)?.MaxCharsPerItem ?? Default.FeatureLength,
            SearchTermCount = Params(SearchTermsRule)?.MaxItems ?? Default.SearchTermCount,
            SearchTermLength = Params(SearchTermsRule)?.MaxCharsPerItem ?? Default.SearchTermLength,
            DesktopScreenshots = Params(DesktopScreenshotsRule)?.MaxCount ?? screenshots?.MaxCount ?? Default.DesktopScreenshots,
            OtherScreenshots = screenshots?.MaxCountOtherFamilies ?? Default.OtherScreenshots,
            ImageCaption = screenshots?.CaptionMaxChars ?? Default.ImageCaption,
        };
    }

    /// <summary>
    /// The limit violations of one listing: field, limit. Empty when the listing fits.
    /// </summary>
    /// <param name="listing"></param>
    /// <returns></returns>
    public List<(string Field, int Limit)> Violations(MsListingDto listing)
    {
        var violations = new List<(string Field, int Limit)>();

        Check(violations, "title", listing.Title, TitleLength);
        Check(violations, "description", listing.Description, Description);
        Check(violations, "shortDescription", listing.ShortDescription, ShortDescription);
        Check(violations, "whatsNew", listing.WhatsNew, WhatsNew);

        if (listing.Features?.Count > FeatureCount)
            violations.Add(("features", FeatureCount));

        if (listing.Features?.Any(f => f == null || f.Length > FeatureLength) == true)
            violations.Add(("features[]", FeatureLength));

        if (listing.SearchTerms?.Count > SearchTermCount)
            violations.Add(("searchTerms", SearchTermCount));

        if (listing.SearchTerms?.Any(t => t == null || t.Length > SearchTermLength) == true)
            violations.Add(("searchTerms[]", SearchTermLength));

        return violations;
    }

    /// <summary>
    /// The limit of the screenshots of <paramref name="imageType"/> per language, or null when the type is not a screenshot.
    /// </summary>
    /// <param name="imageType"></param>
    /// <returns></returns>
    public int? ScreenshotLimit(string imageType)
    {
        if (string.Equals(imageType, MsSubmissionJson.DesktopScreenshotType, StringComparison.OrdinalIgnoreCase))
            return DesktopScreenshots;

        return MsSubmissionJson.OtherScreenshotTypes.Contains(imageType ?? string.Empty) ? OtherScreenshots : null;
    }

    private static void Check(List<(string Field, int Limit)> violations, string field, string value, int limit)
    {
        if (value?.Length > limit)
            violations.Add((field, limit));
    }
}
