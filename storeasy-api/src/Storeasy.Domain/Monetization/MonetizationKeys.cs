using Storeasy.Domain.Content;

namespace Storeasy.Domain.Monetization;

/// <summary>
/// Content keys of store products (<see cref="ContentKey"/> format), used for the diff of a monetization plan and for
/// its fingerprint. The field part is "{owner}/{field}" where the owner is what the member recognizes: the product id,
/// the group reference name, "{productId}:{basePlanId}" on Play.
/// </summary>
public static class MonetizationKeys
{
    /// <summary>
    /// Subscription group values.
    /// </summary>
    public const string Group = "SubscriptionGroup";

    /// <summary>
    /// Subscription and In-App Purchase values (fields, localizations, review details).
    /// </summary>
    public const string Product = "Product";

    /// <summary>
    /// Items of In-App Purchases and one-time products in a plan (their keys use <see cref="Product"/>).
    /// </summary>
    public const string Iap = "InAppPurchase";

    /// <summary>
    /// Prices per territory.
    /// </summary>
    public const string Price = "ProductPrice";

    /// <summary>
    /// Territory availability.
    /// </summary>
    public const string Availability = "ProductAvailability";

    /// <summary>
    /// Offers.
    /// </summary>
    public const string Offer = "Offer";

    /// <summary>
    /// Play base plans and purchase options.
    /// </summary>
    public const string Plan = "BasePlan";

    /// <summary>
    /// Store settings of the app (server notifications, sandbox testers).
    /// </summary>
    public const string Settings = "PurchaseSettings";

    /// <summary>
    /// A content key.
    /// </summary>
    /// <param name="group"></param>
    /// <param name="owner"></param>
    /// <param name="locale"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static string Of(string group, string owner, string locale, string field) => ContentKey.Of(group, locale, $"{owner}/{field}");

    /// <summary>
    /// The owner part of a key's field ("com.x.monthly/name" gives "com.x.monthly").
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string OwnerOf(string key)
    {
        var field = ContentKey.Parse(key).Field ?? string.Empty;
        var slash = field.LastIndexOf('/');

        return slash < 0 ? field : field[..slash];
    }
}
