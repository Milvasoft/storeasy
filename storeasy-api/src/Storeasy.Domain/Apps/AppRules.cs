namespace Storeasy.Domain.Apps;

/// <summary>
/// Pure rules of storeasy apps. No database.
/// </summary>
public static class AppRules
{
    /// <summary>
    /// The key that links the records of one product across stores: the platform identifier (bundle id, package name),
    /// trimmed and lower case. Null when there is none.
    /// </summary>
    /// <param name="bundleId"></param>
    /// <returns></returns>
    public static string LinkKey(string bundleId) => string.IsNullOrWhiteSpace(bundleId) ? null : bundleId.Trim().ToLowerInvariant();
}
