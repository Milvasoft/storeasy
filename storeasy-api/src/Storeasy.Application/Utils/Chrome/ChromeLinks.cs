namespace Storeasy.Application.Utils.Chrome;

/// <summary>
/// Addresses of a Chrome Web Store item.
/// </summary>
public static class ChromeLinks
{
    /// <summary>
    /// The developer dashboard.
    /// </summary>
    public const string DashboardBase = "https://chrome.google.com/webstore/devconsole";

    /// <summary>
    /// The item's store page.
    /// </summary>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public static string StorePage(string itemId) => $"https://chromewebstore.google.com/detail/{Uri.EscapeDataString(itemId ?? string.Empty)}";

    /// <summary>
    /// The item in the developer dashboard (the dashboard itself without a publisher id).
    /// </summary>
    /// <param name="publisherId"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    public static string Dashboard(string publisherId, string itemId)
        => string.IsNullOrEmpty(publisherId) ? DashboardBase : $"{DashboardBase}/{Uri.EscapeDataString(publisherId)}/{Uri.EscapeDataString(itemId ?? string.Empty)}/edit";
}
