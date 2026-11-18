using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;
using System.Globalization;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// Read models of synced store products for the money screens.
/// </summary>
public static class MonetizationViews
{
    private static readonly Dictionary<string, string> _territoryNames = BuildTerritoryNames();

    /// <summary>
    /// Base price of a product.
    /// </summary>
    /// <param name="baseTerritory"></param>
    /// <param name="prices"></param>
    /// <returns></returns>
    public static BasePriceDto BasePrice(string baseTerritory, IReadOnlyList<ProductPrice> prices)
    {
        var price = MonetizationContent.BasePriceOf(baseTerritory, prices);

        return price == null ? null : new BasePriceDto { Territory = price.Territory, Currency = price.Currency, Amount = price.CustomerPrice, Source = price.Source };
    }

    /// <summary>
    /// A Play base plan or purchase option with its offers.
    /// </summary>
    /// <param name="plan"></param>
    /// <param name="offers"></param>
    /// <returns></returns>
    public static BasePlanDto Plan(ProductPlan plan, IEnumerable<ProductOffer> offers) => new()
    {
        BasePlanId = plan.Id,
        Type = plan.Type,
        BillingPeriod = plan.BillingPeriod,
        State = plan.State,
        BasePrice = BasePrice(null, plan.Prices),
        RegionCount = plan.Prices?.Count ?? 0,
        Offers = [.. (offers ?? []).Where(o => o.BasePlanId == plan.Id)],
    };

    /// <summary>
    /// A subscription row of a group.
    /// </summary>
    /// <param name="product"></param>
    /// <returns></returns>
    public static SubscriptionSummaryDto Summary(StoreProduct product) => new()
    {
        Id = product.Id,
        ProductId = product.ProductId,
        ReferenceName = product.ReferenceName,
        Level = product.Level,
        Duration = product.Duration,
        State = product.State,
        StoreState = product.StoreState,
        BasePrice = BasePrice(product.BaseTerritory, product.Prices),
        HasReviewScreenshot = product.ReviewScreenshot != null,
    };

    /// <summary>
    /// A Play subscription.
    /// </summary>
    /// <param name="product"></param>
    /// <returns></returns>
    public static PlaySubscriptionDto PlaySubscription(StoreProduct product) => new()
    {
        Id = product.Id,
        ProductId = product.ProductId,
        State = product.State,
        Listings = product.Localizations,
        BasePlans = [.. (product.Plans ?? []).Select(p => Plan(p, product.Offers))],
    };

    /// <summary>
    /// The detail of a product.
    /// </summary>
    /// <param name="product"></param>
    /// <param name="submitWithVersion"></param>
    /// <returns></returns>
    public static ProductDetailDto Detail(StoreProduct product, bool submitWithVersion) => new()
    {
        Id = product.Id,
        StoreAppId = product.StoreAppId,
        Store = product.Store,
        ExternalId = product.ExternalId,
        Kind = product.Kind,
        ProductId = product.ProductId,
        ReferenceName = product.ReferenceName,
        GroupId = product.GroupId,
        Duration = product.Duration,
        Level = product.Level,
        FamilySharable = product.FamilySharable,
        State = product.State,
        StoreState = product.StoreState,
        Localizations = product.Localizations,
        Availability = product.Availability,
        TerritoryCount = product.Availability?.Territories?.Count ?? product.Prices?.Count ?? 0,
        BasePrice = BasePrice(product.BaseTerritory, product.Prices),
        ReviewScreenshot = product.ReviewScreenshot,
        ReviewNote = product.ReviewNote,
        SubmitWithVersion = submitWithVersion,
        RetentionMessage = product.RetentionMessage,
        MultiSeatStatus = product.MultiSeatStatus,
        Plans = [.. (product.Plans ?? []).Select(p => Plan(p, product.Offers))],
        OfferCount = product.Offers?.Count ?? 0,
        SyncedDate = product.SyncedDate,
    };

    /// <summary>
    /// The offers of a subscription by kind.
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="offers"></param>
    /// <returns></returns>
    public static SubscriptionOffersDto Offers(long productId, IReadOnlyList<ProductOffer> offers) => new()
    {
        SubscriptionId = productId,
        Introductory = [.. (offers ?? []).Where(o => o.Kind == OfferKind.Introductory)],
        Promotional = [.. (offers ?? []).Where(o => o.Kind == OfferKind.Promotional)],
        OfferCodes = [.. (offers ?? []).Where(o => o.Kind == OfferKind.OfferCode)],
        WinBack = [.. (offers ?? []).Where(o => o.Kind == OfferKind.WinBack)],
        PlayOffers = [.. (offers ?? []).Where(o => o.Kind == OfferKind.PlayOffer)],
    };

    /// <summary>
    /// Price table rows of a product (App Store prices, or the prices of one Play base plan or purchase option).
    /// </summary>
    /// <param name="baseTerritory"></param>
    /// <param name="prices"></param>
    /// <returns></returns>
    public static List<ProductPriceDto> Prices(string baseTerritory, IReadOnlyList<ProductPrice> prices)
        => [.. (prices ?? []).Select(p => new ProductPriceDto
        {
            Territory = p.Territory,
            TerritoryName = TerritoryName(p.Territory),
            Currency = p.Currency,
            CustomerPrice = p.CustomerPrice,
            Proceeds = p.Proceeds,
            PricePointId = p.PricePointId,
            Source = p.Source,
            IsBase = string.Equals(p.Territory, baseTerritory, StringComparison.OrdinalIgnoreCase),
            StartDate = p.StartDate,
            HasPendingChange = p.PendingStartDate != null || p.PendingCustomerPrice != null,
            PendingCustomerPrice = p.PendingCustomerPrice,
            PendingStartDate = p.PendingStartDate,
        }).OrderByDescending(p => p.IsBase).ThenBy(p => p.TerritoryName, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// English name of a territory (App Store alpha-3 or Play alpha-2 code); the code when unknown.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static string TerritoryName(string code) => code != null && _territoryNames.TryGetValue(code, out var name) ? name : code;

    /// <summary>
    /// Whether a product must go to review with an app version: on the App Store the app's first subscription or In-App
    /// Purchase (no product of the app was ever on sale) cannot be submitted on its own.
    /// </summary>
    /// <param name="product"></param>
    /// <param name="appProductStates">States of every product of the app.</param>
    /// <returns></returns>
    public static bool SubmitWithVersion(StoreProduct product, IEnumerable<ProductState> appProductStates)
        => product.Store == StoreKind.Apple && !MonetizationRules.WasEverOnSale(product.State) && !appProductStates.Any(MonetizationRules.WasEverOnSale);

    private static Dictionary<string, string> BuildTerritoryNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var regions = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                                 .Select(c => c.Name)
                                 .Where(n => n.Contains('-'))
                                 .Select(n => n[(n.LastIndexOf('-') + 1)..])
                                 .Where(r => r.Length == 2)
                                 .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var code in regions)
        {
            try
            {
                var region = new RegionInfo(code);

                names.TryAdd(region.TwoLetterISORegionName, region.EnglishName);
                names.TryAdd(region.ThreeLetterISORegionName, region.EnglishName);
            }
            catch (ArgumentException)
            {
                // Not a region code.
            }
        }

        return names;
    }
}
