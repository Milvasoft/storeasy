using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Content;
using Storeasy.Domain.Monetization;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Monetization;

public sealed partial class MonetizationPlanner
{
    private static readonly HashSet<string> _playEligibilities = ["NewCustomers", "Upgrade", "DeveloperDetermined"];

    private bool PlaySubscription(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null)
        {
            if (!MonetizationRules.IsValidProductId(StoreKind.Google, change.ProductId))
                return Fail(MessageKey.MonetizationProductIdInvalid, nameof(change.ProductId));

            if (_createdProducts.ContainsKey(change.ProductId))
                return Fail(MessageKey.MonetizationProductIdTaken, change.ProductId);

            if (change.Localizations == null || change.Localizations.Count == 0)
                return Fail(MessageKey.MonetizationLocalizationInvalid, nameof(change.Localizations));
        }
        else if (product.Kind != StoreProductKind.Subscription)
        {
            return Fail(MessageKey.MonetizationProductIdTaken, change.ProductId);
        }

        if (!PlayListings(change, product, true))
            return false;

        if (product == null)
        {
            _createdProducts[change.ProductId] = StoreProductKind.Subscription;
            Item(MonetizationKeys.Product, change.ProductId, null, "productId", null, change.ProductId);
            Call("POST", "/subscriptions", change.ProductId);
        }
        else
        {
            Call("PATCH", $"/subscriptions/{change.ProductId}", "listings");
        }

        _changes.Operations.Add(change.Copy());

        return true;
    }

    private bool OneTimeProduct(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null && !MonetizationRules.IsValidProductId(StoreKind.Google, change.ProductId))
            return Fail(MessageKey.MonetizationProductIdInvalid, nameof(change.ProductId));

        if (product != null && product.Kind != StoreProductKind.OneTimeProduct)
            return Fail(MessageKey.MonetizationProductIdTaken, change.ProductId);

        if (product == null && (change.Localizations == null || change.Localizations.Count == 0 || change.Prices == null || change.Prices.Count == 0))
            return Fail(MessageKey.MonetizationLocalizationInvalid, nameof(change.Localizations));

        var optionId = change.PurchaseOptionId ?? product?.Plans?.FirstOrDefault()?.Id ?? "buy";

        if (!PlanIdPattern().IsMatch(optionId))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.PurchaseOptionId));

        if (!PlayListings(change, product, false) || !PlayPrices(change.Prices))
            return false;

        var owner = MonetizationContent.PlanOwner(change.ProductId, optionId);
        var option = product?.Plans?.FirstOrDefault(p => p.Id == optionId);

        if (product == null)
        {
            _createdProducts[change.ProductId] = StoreProductKind.OneTimeProduct;
            Item(MonetizationKeys.Product, change.ProductId, null, "productId", null, change.ProductId);
        }

        RegionalPrices(owner, option?.Prices, change.Prices);

        if (change.Active != null)
            Item(MonetizationKeys.Plan, owner, null, "state", option?.State.ToString(), change.Active.Value ? ProductState.Active.ToString() : ProductState.Inactive.ToString());

        var op = change.Copy();
        op.PurchaseOptionId = optionId;

        Call("PATCH", $"/onetimeproducts/{change.ProductId}?allowMissing=true", change.ProductId);

        if (change.Active != null)
            Call("POST", $"/oneTimeProducts/{change.ProductId}/purchaseOptions:batchUpdateStates", optionId);

        _changes.Operations.Add(op);

        return true;
    }

    private bool BasePlan(MonetizationChangeDto change)
    {
        if (!PlaySubscriptionOf(change.ProductId))
            return false;

        if (string.IsNullOrEmpty(change.BasePlanId) || !PlanIdPattern().IsMatch(change.BasePlanId))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.BasePlanId));

        var owner = MonetizationContent.PlanOwner(change.ProductId, change.BasePlanId);

        if (FindPlan(change.ProductId, change.BasePlanId) != null || _createdPlans.ContainsKey(owner))
            return Fail(MessageKey.MonetizationBasePlanExists, change.BasePlanId);

        if (string.IsNullOrEmpty(change.Duration) || !IsoPeriodPattern().IsMatch(change.Duration))
            return Fail(MessageKey.MonetizationDurationInvalid, nameof(change.Duration));

        if (change.Prices == null || change.Prices.Count == 0 || !PlayPrices(change.Prices))
            return _errorKey == null ? Fail(MessageKey.MonetizationPriceInvalid, nameof(change.Prices)) : false;

        _createdPlans[owner] = change.AutoRenewing == false ? "Prepaid" : "AutoRenewing";

        Item(MonetizationKeys.Plan, owner, null, "billingPeriod", null, change.Duration);
        Item(MonetizationKeys.Plan, owner, null, "state", null, ProductState.Draft.ToString());
        RegionalPrices(owner, null, change.Prices);

        Call("PATCH", $"/subscriptions/{change.ProductId}?updateMask=basePlans", change.BasePlanId);
        _changes.Operations.Add(change.Copy());

        return true;
    }

    private bool BasePlanState(MonetizationChangeDto change)
    {
        var owner = MonetizationContent.PlanOwner(change.ProductId, change.BasePlanId);
        var plan = FindPlan(change.ProductId, change.BasePlanId);

        if (plan == null && !_createdPlans.ContainsKey(owner))
            return Fail(MessageKey.MonetizationBasePlanNotFound, change.BasePlanId);

        if (change.Active == null)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Active));

        var state = plan?.State ?? ProductState.Draft;

        if (!Item(MonetizationKeys.Plan, owner, null, "state", state.ToString(), change.Active.Value ? ProductState.Active.ToString() : ProductState.Inactive.ToString()))
            return true;

        if (change.Active == false)
            Warn(MessageKey.MonetizationBasePlanDeactivated, owner);

        Call("POST", $"/subscriptions/{change.ProductId}/basePlans/{change.BasePlanId}:{(change.Active.Value ? "activate" : "deactivate")}", owner);
        _changes.Operations.Add(change.Copy());

        return true;
    }

    private bool BasePlanPrices(MonetizationChangeDto change)
    {
        var plan = FindPlan(change.ProductId, change.BasePlanId);

        if (plan == null)
            return Fail(MessageKey.MonetizationBasePlanNotFound, change.BasePlanId);

        if (change.Prices == null || change.Prices.Count == 0 || !PlayPrices(change.Prices))
            return _errorKey == null ? Fail(MessageKey.MonetizationPriceInvalid, nameof(change.Prices)) : false;

        var owner = MonetizationContent.PlanOwner(change.ProductId, change.BasePlanId);
        var onSale = MonetizationRules.IsOnSale(plan.State);

        if (!OnSalePrice(owner, onSale, change.PreserveCurrentPrice, StoreProductKind.Subscription))
            return false;

        var increase = RegionalPrices(owner, plan.Prices, change.Prices);

        if (increase && onSale)
            Warn(MessageKey.MonetizationPriceIncreaseConsent, owner);

        Call("PATCH", $"/subscriptions/{change.ProductId}?updateMask=basePlans", $"{owner}: {change.Prices.Count} regions");

        if (onSale && change.PreserveCurrentPrice == false)
            Call("POST", $"/subscriptions/{change.ProductId}/basePlans/{change.BasePlanId}:migratePrices", owner);

        _changes.Operations.Add(change.Copy());

        return true;
    }

    private bool PlayOffer(MonetizationChangeDto change)
    {
        var owner = MonetizationContent.PlanOwner(change.ProductId, change.BasePlanId);
        var plan = FindPlan(change.ProductId, change.BasePlanId);
        var type = plan?.Type ?? _createdPlans.GetValueOrDefault(owner);

        if (type == null)
            return Fail(MessageKey.MonetizationBasePlanNotFound, change.BasePlanId);

        if (type != "AutoRenewing")
            return Fail(MessageKey.MonetizationChangeUnsupported, change.BasePlanId);

        if (string.IsNullOrEmpty(change.Code) || !PlanIdPattern().IsMatch(change.Code))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Code));

        var product = Snapshot.Product(change.ProductId);
        var field = $"{OfferKind.PlayOffer}:{change.Code}";

        Base(MonetizationKeys.Offer, owner, null, field);

        if ((product?.Offers ?? []).Any(o => o.BasePlanId == change.BasePlanId && o.Code == change.Code))
            return Fail(MessageKey.MonetizationOfferExists, change.Code);

        if (change.Phases == null || change.Phases.Count is < 1 or > 2 || change.Phases.Any(p => string.IsNullOrEmpty(p.Duration) || !IsoPeriodPattern().IsMatch(p.Duration) || p.Recurrence < 1))
            return Fail(MessageKey.MonetizationOfferInvalid, nameof(change.Phases));

        foreach (var phase in change.Phases.Where(p => !p.Free))
        {
            if (phase.Prices == null || phase.Prices.Count == 0 || !PlayPrices(phase.Prices))
                return _errorKey == null ? Fail(MessageKey.MonetizationPriceInvalid, nameof(change.Phases)) : false;
        }

        if (change.Eligibility?.Any(e => !_playEligibilities.Contains(e)) == true)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Eligibility));

        var op = change.Copy();
        op.Eligibility = change.Eligibility?.Count > 0 ? change.Eligibility : ["NewCustomers"];
        op.Territories = change.Territories?.Count > 0 ? change.Territories : [.. (plan?.Prices ?? []).Select(p => p.Territory)];

        if (op.Territories.Count == 0)
            op.Territories = [.. change.Phases.Where(p => p.Prices != null).SelectMany(p => p.Prices).Select(p => p.Territory).Distinct()];

        if (op.Territories.Count == 0)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Territories));

        Offer(owner, field, string.Join(" + ", change.Phases.Select(p => $"{(p.Free ? "FreeTrial" : "Price")} {p.Duration} x{p.Recurrence}")));
        Call("POST", $"/subscriptions/{change.ProductId}/basePlans/{change.BasePlanId}/offers", change.Code);
        _changes.Operations.Add(op);

        return true;
    }

    private bool PlayOfferState(MonetizationChangeDto change)
    {
        var owner = MonetizationContent.PlanOwner(change.ProductId, change.BasePlanId);
        var offer = Snapshot.Product(change.ProductId)?.Offers?.FirstOrDefault(o => o.BasePlanId == change.BasePlanId && o.Code == change.Code);

        if (offer == null)
            return Fail(MessageKey.MonetizationOfferNotFound, change.Code);

        if (change.Active == null)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Active));

        if (!Item(MonetizationKeys.Offer, owner, null, $"{OfferKind.PlayOffer}:{change.Code}", offer.State, change.Active.Value ? "ACTIVE" : "INACTIVE"))
            return true;

        Call("POST", $"/subscriptions/{change.ProductId}/basePlans/{change.BasePlanId}/offers/{change.Code}:{(change.Active.Value ? "activate" : "deactivate")}", change.Code);
        _changes.Operations.Add(change.Copy());

        return true;
    }

    private bool PlaySubscriptionOf(string productId)
    {
        var product = Snapshot.Product(productId);

        if (product?.Kind == StoreProductKind.Subscription || _createdProducts.GetValueOrDefault(productId ?? string.Empty) == StoreProductKind.Subscription)
            return true;

        return Fail(MessageKey.MonetizationProductNotFound, productId);
    }

    private ProductPlan FindPlan(string productId, string planId) => Snapshot.Product(productId)?.Plans?.FirstOrDefault(p => p.Id == planId);

    private bool PlayListings(MonetizationChangeDto change, StoreProduct product, bool subscription)
    {
        foreach (var listing in change.Localizations ?? [])
        {
            if (string.IsNullOrWhiteSpace(listing.Locale) || string.IsNullOrWhiteSpace(listing.Name) || (!subscription && string.IsNullOrWhiteSpace(listing.Description)))
                return Fail(MessageKey.MonetizationLocalizationInvalid, listing.Locale ?? nameof(change.Localizations));

            if (ContentDiff.CharCount(listing.Name) > MonetizationRules.PlayTitleMaxLength || ContentDiff.CharCount(listing.Description) > MonetizationRules.PlayDescriptionMaxLength)
                return Fail(MessageKey.MonetizationTextTooLong, listing.Locale);

            var current = product?.Localizations?.FirstOrDefault(l => l.Locale == listing.Locale);

            Item(MonetizationKeys.Product, change.ProductId, listing.Locale, "name", current?.Name, listing.Name);
            Item(MonetizationKeys.Product, change.ProductId, listing.Locale, "description", current?.Description, listing.Description);

            if (subscription && listing.Benefits != null)
                Item(MonetizationKeys.Product, change.ProductId, listing.Locale, "benefits", current?.Benefits == null ? null : string.Join("\n", current.Benefits), string.Join("\n", listing.Benefits));
        }

        return true;
    }

    private bool PlayPrices(IReadOnlyList<PriceInputDto> prices)
    {
        foreach (var price in prices ?? [])
        {
            if (string.IsNullOrWhiteSpace(price.Territory) || string.IsNullOrWhiteSpace(price.Currency) || !MonetizationRules.IsValidAmount(price.Currency, price.Amount))
                return Fail(MessageKey.MonetizationPriceInvalid, $"Prices[{price.Territory}].Amount");
        }

        return true;
    }

    /// <summary>
    /// Adds an item per region whose price changes; returns whether one of them increases.
    /// </summary>
    private bool RegionalPrices(string owner, List<ProductPrice> current, List<PriceInputDto> prices)
    {
        var increase = false;

        foreach (var price in prices ?? [])
        {
            var region = price.Territory.Trim().ToUpperInvariant();
            var before = current?.FirstOrDefault(p => string.Equals(p.Territory, region, StringComparison.OrdinalIgnoreCase));

            if (Item(MonetizationKeys.Price, owner, region, "price", before == null ? null : MonetizationContent.PriceText(before.CustomerPrice, before.Currency), MonetizationContent.PriceText(price.Amount, price.Currency)))
                increase |= MonetizationRules.IsIncrease(before?.CustomerPrice, price.Amount);
        }

        return increase;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$")]
    private static partial Regex PlanIdPattern();

    [GeneratedRegex("^P(\\d+[YMWD])+$")]
    private static partial Regex IsoPeriodPattern();
}
