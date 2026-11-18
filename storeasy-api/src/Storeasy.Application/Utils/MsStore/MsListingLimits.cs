using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Microsoft Store listing limits, from the rule catalog (<c>rules/microsoft.json</c>, category <c>listing-limits</c> and
/// <c>screenshots</c>). Kept as constants until the catalog is served by the API; each constant names its rule id.
/// </summary>
public static class MsListingLimits
{
    /// <summary>microsoft.listing-limits.description</summary>
    public const int Description = 10000;

    /// <summary>microsoft.listing-limits.short-description</summary>
    public const int ShortDescription = 1000;

    /// <summary>microsoft.listing-limits.whats-new</summary>
    public const int WhatsNew = 1500;

    /// <summary>microsoft.listing-limits.product-features (count)</summary>
    public const int FeatureCount = 20;

    /// <summary>microsoft.listing-limits.product-features (characters each)</summary>
    public const int FeatureLength = 200;

    /// <summary>microsoft.listing-limits.search-terms (count)</summary>
    public const int SearchTermCount = 7;

    /// <summary>microsoft.listing-limits.search-terms (characters each)</summary>
    public const int SearchTermLength = 30;

    /// <summary>Title of an app listing (reserved name length in Partner Center).</summary>
    public const int Title = 256;

    /// <summary>microsoft.screenshots.desktop (per language)</summary>
    public const int DesktopScreenshots = 10;

    /// <summary>microsoft.screenshots.general (other device families, per language)</summary>
    public const int OtherScreenshots = 8;

    /// <summary>microsoft.screenshots.general (caption)</summary>
    public const int ImageCaption = 200;

    /// <summary>Add-on listing title.</summary>
    public const int AddonTitle = 100;

    /// <summary>Add-on listing description.</summary>
    public const int AddonDescription = 200;

    /// <summary>
    /// The limit violations of one listing: field, limit. Empty when the listing fits.
    /// </summary>
    /// <param name="listing"></param>
    /// <returns></returns>
    public static List<(string Field, int Limit)> Violations(MsListingDto listing)
    {
        var violations = new List<(string Field, int Limit)>();

        Check(violations, "title", listing.Title, Title);
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
    public static int? ScreenshotLimit(string imageType)
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
