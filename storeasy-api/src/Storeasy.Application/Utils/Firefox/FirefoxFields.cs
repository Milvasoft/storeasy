namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// The listing fields of a Firefox add-on storeasy reads and writes, with the AMO API name and limit of each, and the
/// categories of Firefox extensions.
/// </summary>
public static class FirefoxFields
{
    /// <summary>Add-on name.</summary>
    public const string Name = "Name";

    /// <summary>Summary (shown under the name and in search).</summary>
    public const string Summary = "Summary";

    /// <summary>Description (the long text of the add-on page).</summary>
    public const string Description = "Description";

    /// <summary>Homepage address.</summary>
    public const string Homepage = "Homepage";

    /// <summary>Support site address.</summary>
    public const string SupportUrl = "SupportUrl";

    /// <summary>Support e-mail address.</summary>
    public const string SupportEmail = "SupportEmail";

    /// <summary>Release notes of a version (plan texts only).</summary>
    public const string ReleaseNotes = "ReleaseNotes";

    /// <summary>
    /// Most characters of a description storeasy sends (AMO publishes no limit; this keeps a plan readable).
    /// </summary>
    public const int DescriptionMaxLength = 15000;

    /// <summary>
    /// Most characters of release notes storeasy sends.
    /// </summary>
    public const int ReleaseNotesMaxLength = 3000;

    /// <summary>
    /// Most characters of notes to the reviewer.
    /// </summary>
    public const int ApprovalNotesMaxLength = 3000;

    /// <summary>
    /// The listing fields, in the order they are shown, with the AMO API name and the longest value.
    /// </summary>
    public static readonly IReadOnlyList<(string Field, string ApiName, int MaxLength)> Listing =
    [
        (Name, "name", FirefoxManifest.NameMaxLength),
        (Summary, "summary", FirefoxManifest.SummaryMaxLength),
        (Description, "description", DescriptionMaxLength),
        (Homepage, "homepage", 255),
        (SupportUrl, "support_url", 255),
        (SupportEmail, "support_email", 100),
    ];

    /// <summary>
    /// Category slugs of Firefox extensions on AMO.
    /// </summary>
    public static readonly IReadOnlyList<string> Categories =
    [
        "alerts-updates", "appearance", "bookmarks", "download-management", "feeds-news-blogging", "games-entertainment",
        "language-support", "photos-music-videos", "privacy-security", "search-tools", "shopping", "social-communication",
        "tabs", "web-development", "other",
    ];

    /// <summary>
    /// The row of <paramref name="field"/> (case insensitive), or null.
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static (string Field, string ApiName, int MaxLength)? Of(string field)
        => Listing.FirstOrDefault(f => string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase) || string.Equals(f.ApiName, field, StringComparison.OrdinalIgnoreCase)) is { Field: not null } row ? row : null;

    /// <summary>
    /// Whether the field holds an address.
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static bool IsUrl(string field) => field is Homepage or SupportUrl;
}
