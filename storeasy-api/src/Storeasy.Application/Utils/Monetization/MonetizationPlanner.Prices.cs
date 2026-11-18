using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Utils.Monetization;

public sealed partial class MonetizationPlanner
{
    private bool BasePrice(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (!PriceRequest(change, product))
            return false;

        var territory = change.Territory.Trim().ToUpperInvariant();
        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.ProductKind = product.Kind;
        op.Territory = territory;

        if (product.Kind != StoreProductKind.Subscription)
            return IapPrice(product, op, territory, change.PricePointId, true);

        List<PriceInputDto> prices = [new PriceInputDto { Territory = territory, PricePointId = change.PricePointId, CustomerPrice = PriceTextOf(change.PricePointId) }];

        if (change.Equalize)
        {
            if (_input.Equalizations == null || !_input.Equalizations.TryGetValue(change.PricePointId, out var equalized))
                return Fail(MessageKey.MonetizationPricePointRequired, nameof(change.PricePointId));

            var manual = 0;

            foreach (var point in equalized.Where(p => !string.Equals(p.Territory, territory, StringComparison.OrdinalIgnoreCase)))
            {
                var current = product.Prices?.FirstOrDefault(p => string.Equals(p.Territory, point.Territory, StringComparison.OrdinalIgnoreCase));

                if (current?.Source == PriceSource.Manual)
                {
                    manual++;
                    continue;
                }

                prices.Add(new PriceInputDto { Territory = point.Territory, PricePointId = point.Id, CustomerPrice = MonetizationContent.PriceText(point.CustomerPrice, point.Currency) });
            }

            if (manual > 0)
                Warn(MessageKey.MonetizationManualPricesKept, manual);
        }

        Base(MonetizationKeys.Price, product.ProductId, null, "baseTerritory");

        return SubscriptionPrices(product, op, prices);
    }

    private bool TerritoryPrice(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (!PriceRequest(change, product))
            return false;

        var territory = change.Territory.Trim().ToUpperInvariant();
        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.ProductKind = product.Kind;
        op.Territory = territory;

        if (product.Kind != StoreProductKind.Subscription)
            return IapPrice(product, op, territory, change.PricePointId, false);

        return SubscriptionPrices(product, op, [new PriceInputDto { Territory = territory, PricePointId = change.PricePointId, CustomerPrice = PriceTextOf(change.PricePointId) }]);
    }

    /// <summary>
    /// Checks the common fields of a price change and applies the rules of a product on sale: the stronger permission, an
    /// explicit choice for existing subscribers and, when the new price reaches them, a typed confirmation.
    /// </summary>
    private bool PriceRequest(MonetizationChangeDto change, StoreProduct product)
    {
        if (string.IsNullOrWhiteSpace(change.Territory) || string.IsNullOrWhiteSpace(change.PricePointId))
            return Fail(MessageKey.MonetizationPricePointRequired, nameof(change.PricePointId));

        if (change.StartDate != null && change.StartDate < _input.Today)
            return Fail(MessageKey.MonetizationStartDateInPast, nameof(change.StartDate));

        return OnSalePrice(product.ProductId, MonetizationRules.IsOnSale(product.State), change.PreserveCurrentPrice, product.Kind == StoreProductKind.Subscription || product.Kind == StoreProductKind.OneTimeProduct ? product.Kind : null);
    }

    private bool OnSalePrice(string productId, bool onSale, bool? preserveCurrentPrice, StoreProductKind? subscribers)
    {
        if (!onSale)
            return true;

        Require(MonetizationRules.PricePermission(true));

        if (subscribers != StoreProductKind.Subscription)
            return true;

        if (preserveCurrentPrice == null)
            return Fail(MessageKey.MonetizationPreserveChoiceRequired, nameof(MonetizationChangeDto.PreserveCurrentPrice));

        if (preserveCurrentPrice.Value)
        {
            Warn(MessageKey.MonetizationExistingSubscribersKept, productId);
        }
        else
        {
            _confirmText ??= MonetizationRules.PriceChangeConfirmText(_input.App.AppName, productId);
            Warn(MessageKey.MonetizationExistingSubscribersMigrated, productId);
        }

        return true;
    }

    private bool SubscriptionPrices(StoreProduct product, MonetizationChangeDto op, List<PriceInputDto> prices)
    {
        var written = new List<PriceInputDto>();
        var increase = false;

        foreach (var price in prices)
        {
            var current = product.Prices?.FirstOrDefault(p => string.Equals(p.Territory, price.Territory, StringComparison.OrdinalIgnoreCase));
            var before = current == null ? null : MonetizationContent.PriceText(current.CustomerPrice, current.Currency);

            Base(MonetizationKeys.Price, product.ProductId, price.Territory, "pricePoint");

            if (current?.PricePointId == price.PricePointId && op.StartDate == null)
                continue;

            increase |= MonetizationRules.IsIncrease(current?.CustomerPrice, price.CustomerPrice?.Split(' ')[0]);

            _draft.Items.Add(new PlanItem
            {
                Group = MonetizationKeys.Price,
                Locale = price.Territory,
                Field = $"{product.ProductId}/price",
                Before = before,
                After = price.CustomerPrice ?? price.PricePointId,
                Action = current == null ? ChangeAction.Create : ChangeAction.Update,
            });

            written.Add(price);
        }

        if (written.Count == 0)
            return true;

        if (increase && MonetizationRules.IsOnSale(product.State))
            Warn(MessageKey.MonetizationPriceIncreaseConsent, product.ProductId);

        op.Prices = written;

        Call("POST", "/v1/subscriptionPrices", $"{product.ProductId}: {written.Count} territories");
        _changes.Operations.Add(op);

        return true;
    }

    /// <summary>
    /// An In-App Purchase has one price schedule: a base territory price (the store equalizes the rest) plus territories
    /// set by hand. A new schedule replaces the old one, so the manual prices that stay are written again.
    /// </summary>
    private bool IapPrice(StoreProduct product, MonetizationChangeDto op, string territory, string pricePointId, bool isBase)
    {
        var baseTerritory = isBase ? territory : product.BaseTerritory;
        var basePrice = MonetizationContent.BasePriceOf(product.BaseTerritory, product.Prices);
        var basePoint = isBase ? pricePointId : basePrice?.PricePointId;

        if (baseTerritory == null || basePoint == null)
            return Fail(MessageKey.MonetizationPricePointRequired, nameof(MonetizationChangeDto.Territory));

        var manual = (product.Prices ?? []).Where(p => p.Source == PriceSource.Manual && !string.Equals(p.Territory, baseTerritory, StringComparison.OrdinalIgnoreCase) && !string.Equals(p.Territory, territory, StringComparison.OrdinalIgnoreCase))
                                           .Select(p => new PriceInputDto { Territory = p.Territory, PricePointId = p.PricePointId })
                                           .ToList();

        if (!isBase)
            manual.Add(new PriceInputDto { Territory = territory, PricePointId = pricePointId });

        var current = product.Prices?.FirstOrDefault(p => string.Equals(p.Territory, territory, StringComparison.OrdinalIgnoreCase));

        Base(MonetizationKeys.Price, product.ProductId, null, "baseTerritory");
        Base(MonetizationKeys.Price, product.ProductId, territory, "pricePoint");

        if (current?.PricePointId == pricePointId && (!isBase || string.Equals(product.BaseTerritory, territory, StringComparison.OrdinalIgnoreCase)) && op.StartDate == null)
            return true;

        _draft.Items.Add(new PlanItem
        {
            Group = MonetizationKeys.Price,
            Locale = territory,
            Field = $"{product.ProductId}/{(isBase ? "basePrice" : "price")}",
            Before = current == null ? null : MonetizationContent.PriceText(current.CustomerPrice, current.Currency),
            After = PriceTextOf(pricePointId) ?? pricePointId,
            Action = current == null ? ChangeAction.Create : ChangeAction.Update,
        });

        op.Territory = baseTerritory;
        op.PricePointId = basePoint;
        op.Prices = manual;

        Call("POST", "/v1/inAppPurchasePriceSchedules", $"{product.ProductId} ({baseTerritory})");
        _changes.Operations.Add(op);

        return true;
    }

    private string PriceTextOf(string pricePointId)
    {
        if (pricePointId == null)
            return null;

        if (_input.PricePoints != null && _input.PricePoints.TryGetValue(pricePointId, out var point))
            return MonetizationContent.PriceText(point.CustomerPrice, point.Currency);

        var equalized = _input.Equalizations?.Values.SelectMany(v => v).FirstOrDefault(p => p.Id == pricePointId);

        return equalized == null ? null : MonetizationContent.PriceText(equalized.CustomerPrice, equalized.Currency);
    }
}
