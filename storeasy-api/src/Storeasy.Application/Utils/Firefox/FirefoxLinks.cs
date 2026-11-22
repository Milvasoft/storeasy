using System.Globalization;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// Addresses of a Firefox add-on.
/// </summary>
public static class FirefoxLinks
{
    /// <summary>
    /// The add-ons of the Developer Hub.
    /// </summary>
    public const string DeveloperHub = "https://addons.mozilla.org/developers/addons";

    /// <summary>
    /// Where API keys are created.
    /// </summary>
    public const string ApiKeys = "https://addons.mozilla.org/developers/addon/api/key/";

    /// <summary>
    /// The add-on page (by slug, else by id).
    /// </summary>
    /// <param name="slug"></param>
    /// <param name="addonId"></param>
    /// <returns></returns>
    public static string StorePage(string slug, long addonId)
        => $"https://addons.mozilla.org/firefox/addon/{Uri.EscapeDataString(string.IsNullOrEmpty(slug) ? addonId.ToString(CultureInfo.InvariantCulture) : slug)}/";

    /// <summary>
    /// The add-on in the Developer Hub.
    /// </summary>
    /// <param name="slug"></param>
    /// <param name="addonId"></param>
    /// <returns></returns>
    public static string Edit(string slug, long addonId)
        => $"https://addons.mozilla.org/developers/addon/{Uri.EscapeDataString(string.IsNullOrEmpty(slug) ? addonId.ToString(CultureInfo.InvariantCulture) : slug)}/edit";
}
