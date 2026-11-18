using Storeasy.Domain.Content;
using Storeasy.Domain.Monetization;
using System.Globalization;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// The store products of an app as content keys (<see cref="MonetizationKeys"/>): what a monetization plan diffs and
/// fingerprints, and what the approval reads again to refuse a plan whose base changed in the store meanwhile.
/// </summary>
public static class MonetizationContent
{
    /// <summary>
    /// Owner of the app wide purchase settings.
    /// </summary>
    public const string AppOwner = "app";

    /// <summary>
    /// Every value of <paramref name="snapshot"/> by content key.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static Dictionary<string, string> Flatten(MonetizationSnapshot snapshot)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in snapshot.Groups)
        {
            values[MonetizationKeys.Of(MonetizationKeys.Group, group.ReferenceName, null, "referenceName")] = group.ReferenceName;

            foreach (var localization in group.Localizations ?? [])
            {
                values[MonetizationKeys.Of(MonetizationKeys.Group, group.ReferenceName, localization.Locale, "name")] = localization.Name;
                values[MonetizationKeys.Of(MonetizationKeys.Group, group.ReferenceName, localization.Locale, "customAppName")] = localization.CustomAppName;
            }
        }

        foreach (var product in snapshot.Products)
            AddProduct(values, snapshot, product);

        values[MonetizationKeys.Of(MonetizationKeys.Settings, AppOwner, null, "notificationUrl")] = snapshot.NotificationUrl;
        values[MonetizationKeys.Of(MonetizationKeys.Settings, AppOwner, null, "notificationVersion")] = snapshot.NotificationVersion;
        values[MonetizationKeys.Of(MonetizationKeys.Settings, AppOwner, null, "sandboxNotificationUrl")] = snapshot.SandboxNotificationUrl;
        values[MonetizationKeys.Of(MonetizationKeys.Settings, AppOwner, null, "sandboxNotificationVersion")] = snapshot.SandboxNotificationVersion;

        return values;
    }

    /// <summary>
    /// The snapshot as a content snapshot (values only), for the change plan service's fingerprint.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static StoreContentSnapshot AsContent(MonetizationSnapshot snapshot)
    {
        var content = new StoreContentSnapshot { Store = snapshot.Store, ExternalId = snapshot.ExternalId };

        foreach (var (key, value) in Flatten(snapshot))
            content.Values[key] = value;

        return content;
    }

    /// <summary>
    /// What to read so that <paramref name="keys"/> have their values: the parts and the product ids (null: products are
    /// not limited).
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    public static (MonetizationParts Parts, HashSet<string> ProductIds) ScopeOf(IEnumerable<string> keys)
    {
        var parts = MonetizationParts.Products;
        var productIds = new HashSet<string>(StringComparer.Ordinal);
        var unlimited = false;

        foreach (var key in keys ?? [])
        {
            var group = ContentKey.Parse(key).Group;
            var owner = MonetizationKeys.OwnerOf(key);

            parts |= group switch
            {
                MonetizationKeys.Price or MonetizationKeys.Plan => MonetizationParts.Prices,
                MonetizationKeys.Offer => MonetizationParts.Offers,
                MonetizationKeys.Availability => MonetizationParts.Availability,
                MonetizationKeys.Group or MonetizationKeys.Product => MonetizationParts.Localizations | MonetizationParts.Review,
                _ => MonetizationParts.Products,
            };

            if (group == MonetizationKeys.Group)
                unlimited = true;
            else if (group != MonetizationKeys.Settings)
                productIds.Add(ProductIdOf(owner));
        }

        return (parts, unlimited ? null : productIds);
    }

    /// <summary>
    /// The product id of an owner ("halka_premium:aylik" gives "halka_premium").
    /// </summary>
    /// <param name="owner"></param>
    /// <returns></returns>
    public static string ProductIdOf(string owner)
    {
        var colon = owner?.IndexOf(':') ?? -1;

        return colon < 0 ? owner : owner[..colon];
    }

    /// <summary>
    /// Owner of a Play base plan or purchase option.
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="planId"></param>
    /// <returns></returns>
    public static string PlanOwner(string productId, string planId) => $"{productId}:{planId}";

    /// <summary>
    /// Price text of a store price ("49.99 TRY").
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="currency"></param>
    /// <returns></returns>
    public static string PriceText(string amount, string currency)
        => string.IsNullOrEmpty(amount) ? null : string.IsNullOrEmpty(currency) ? amount : $"{amount} {currency}";

    /// <summary>
    /// Price in the base territory (or the first price) of a product.
    /// </summary>
    /// <param name="baseTerritory"></param>
    /// <param name="prices"></param>
    /// <returns></returns>
    public static ProductPrice BasePriceOf(string baseTerritory, IReadOnlyList<ProductPrice> prices)
    {
        if (prices == null || prices.Count == 0)
            return null;

        return prices.FirstOrDefault(p => string.Equals(p.Territory, baseTerritory, StringComparison.OrdinalIgnoreCase))
               ?? prices.FirstOrDefault(p => p.Territory is "USA" or "US")
               ?? prices[0];
    }

    /// <summary>
    /// Text of an introductory offer for the diff ("FreeTrial ONE_WEEK x1").
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="duration"></param>
    /// <param name="periods"></param>
    /// <returns></returns>
    public static string OfferText(OfferMode? mode, string duration, int? periods)
        => mode == null ? null : string.Create(CultureInfo.InvariantCulture, $"{mode} {duration} x{periods ?? 1}");

    private static void AddProduct(Dictionary<string, string> values, MonetizationSnapshot snapshot, StoreProduct product)
    {
        var owner = product.ProductId;

        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "productId")] = product.ProductId;
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "name")] = product.ReferenceName;
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "familySharable")] = product.FamilySharable?.ToString();
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "reviewNote")] = product.ReviewNote;
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "level")] = product.Level?.ToString(CultureInfo.InvariantCulture);
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "multiSeatStatus")] = product.MultiSeatStatus;
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "reviewScreenshot")] = product.ReviewScreenshot?.Checksum ?? product.ReviewScreenshot?.FileName;
        values[MonetizationKeys.Of(MonetizationKeys.Product, owner, null, "state")] = product.StoreState;

        foreach (var localization in product.Localizations ?? [])
        {
            values[MonetizationKeys.Of(MonetizationKeys.Product, owner, localization.Locale, "name")] = localization.Name;
            values[MonetizationKeys.Of(MonetizationKeys.Product, owner, localization.Locale, "description")] = localization.Description;

            if (localization.Benefits != null)
                values[MonetizationKeys.Of(MonetizationKeys.Product, owner, localization.Locale, "benefits")] = string.Join("\n", localization.Benefits);
        }

        if (snapshot.Parts.HasFlag(MonetizationParts.Availability) && product.Availability != null)
        {
            values[MonetizationKeys.Of(MonetizationKeys.Availability, owner, null, "territories")] = string.Join(",", product.Availability.Territories.Order(StringComparer.Ordinal));
            values[MonetizationKeys.Of(MonetizationKeys.Availability, owner, null, "availableInNewTerritories")] = product.Availability.AvailableInNewTerritories?.ToString();
        }

        values[MonetizationKeys.Of(MonetizationKeys.Price, owner, null, "baseTerritory")] = product.BaseTerritory;

        foreach (var price in product.Prices ?? [])
        {
            values[MonetizationKeys.Of(MonetizationKeys.Price, owner, price.Territory, "pricePoint")] = price.PricePointId ?? PriceText(price.CustomerPrice, price.Currency);
            values[MonetizationKeys.Of(MonetizationKeys.Price, owner, price.Territory, "pending")] = price.PendingPricePointId ?? price.PendingCustomerPrice;
        }

        foreach (var offer in product.Offers ?? [])
        {
            if (offer.Kind == OfferKind.Introductory)
            {
                foreach (var territory in offer.Territories ?? [])
                    values[MonetizationKeys.Of(MonetizationKeys.Offer, owner, territory, "introductory")] = OfferText(offer.Mode, offer.Duration, offer.Periods);
            }
            else
            {
                var offerOwner = offer.BasePlanId == null ? owner : PlanOwner(owner, offer.BasePlanId);

                values[MonetizationKeys.Of(MonetizationKeys.Offer, offerOwner, null, $"{offer.Kind}:{offer.Code ?? offer.Name ?? offer.Id}")] = offer.State ?? OfferText(offer.Mode, offer.Duration, offer.Periods);
            }
        }

        foreach (var plan in product.Plans ?? [])
        {
            var planOwner = PlanOwner(owner, plan.Id);

            values[MonetizationKeys.Of(MonetizationKeys.Plan, planOwner, null, "state")] = plan.State.ToString();
            values[MonetizationKeys.Of(MonetizationKeys.Plan, planOwner, null, "billingPeriod")] = plan.BillingPeriod;

            foreach (var price in plan.Prices ?? [])
                values[MonetizationKeys.Of(MonetizationKeys.Price, planOwner, price.Territory, "price")] = PriceText(price.CustomerPrice, price.Currency);
        }
    }
}
