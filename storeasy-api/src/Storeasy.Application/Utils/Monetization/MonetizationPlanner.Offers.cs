using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Utils.Monetization;

public sealed partial class MonetizationPlanner
{
    private static readonly HashSet<string> _customerEligibilities = ["NEW", "EXISTING", "EXPIRED"];

    private bool IntroductoryOffer(MonetizationChangeDto change)
    {
        if (!OfferProduct(change, out var product))
            return false;

        var paid = change.Mode != OfferMode.FreeTrial;
        var requested = paid
            ? (change.Prices ?? []).Select(p => p.Territory?.Trim().ToUpperInvariant()).ToList()
            : (change.Territories ?? product.Availability?.Territories ?? [.. (product.Prices ?? []).Select(p => p.Territory)]).Select(t => t?.Trim().ToUpperInvariant()).ToList();

        if (requested.Count == 0 || requested.Any(string.IsNullOrEmpty))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Territories));

        if (paid && change.Prices.Any(p => string.IsNullOrWhiteSpace(p.PricePointId)))
            return Fail(MessageKey.MonetizationPricePointRequired, nameof(change.Prices));

        if (change.StartDate != null && change.StartDate < _input.Today)
            return Fail(MessageKey.MonetizationStartDateInPast, nameof(change.StartDate));

        var existing = (product.Offers ?? []).Where(o => o.Kind == OfferKind.Introductory).SelectMany(o => o.Territories ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var territories = new List<string>();

        foreach (var territory in requested.Distinct())
        {
            Base(MonetizationKeys.Offer, product.ProductId, territory, "introductory");

            if (existing.Contains(territory))
                Warn(MessageKey.MonetizationIntroOfferExists, territory);
            else
                territories.Add(territory);
        }

        if (territories.Count == 0)
            return Fail(MessageKey.MonetizationOfferExists, product.ProductId);

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.Territories = territories;
        op.Prices = paid ? [.. change.Prices.Where(p => territories.Contains(p.Territory?.Trim().ToUpperInvariant()))] : null;

        Offer(product.ProductId, "introductory", $"{MonetizationContent.OfferText(change.Mode, change.Duration, change.Periods)} ({territories.Count} territories)");
        Call("POST", "/v1/subscriptionIntroductoryOffers", $"{product.ProductId}: {territories.Count} territories");
        _changes.Operations.Add(op);

        return true;
    }

    private bool PromotionalOffer(MonetizationChangeDto change)
    {
        if (!OfferProduct(change, out var product))
            return false;

        if (!OfferIdentity(change, product, OfferKind.Promotional) || !OfferPrices(change))
            return false;

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;

        Offer(product.ProductId, $"{OfferKind.Promotional}:{change.Code}", $"{change.ReferenceName}: {MonetizationContent.OfferText(change.Mode, change.Duration, change.Periods)}");
        Call("POST", "/v1/subscriptionPromotionalOffers", $"{product.ProductId} {change.Code}");
        _changes.Operations.Add(op);

        return true;
    }

    private bool OfferCode(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null || product.Kind != StoreProductKind.Subscription)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (string.IsNullOrWhiteSpace(change.ReferenceName))
            return Fail(MessageKey.MonetizationReferenceNameRequired, nameof(change.ReferenceName));

        var existing = (product.Offers ?? []).FirstOrDefault(o => o.Kind == OfferKind.OfferCode && string.Equals(o.Name, change.ReferenceName, StringComparison.Ordinal));
        var field = $"{OfferKind.OfferCode}:{change.ReferenceName}";

        Base(MonetizationKeys.Offer, product.ProductId, null, field);

        if (change.Active == false)
        {
            if (existing == null)
                return Fail(MessageKey.MonetizationOfferNotFound, change.ReferenceName);

            if (!Item(MonetizationKeys.Offer, product.ProductId, null, field, existing.State, "False", fingerprint: false))
                return true;

            var deactivate = change.Copy();
            deactivate.ProductExternalId = product.ExternalId;
            deactivate.Code = existing.Id;

            Call("PATCH", $"/v1/subscriptionOfferCodes/{existing.Id}", change.ReferenceName);
            _changes.Operations.Add(deactivate);

            return true;
        }

        if (existing != null)
            return Fail(MessageKey.MonetizationOfferExists, change.ReferenceName);

        if (!ValidOffer(change, product) || !OfferPrices(change))
            return false;

        if (change.Eligibility == null || change.Eligibility.Count == 0 || change.Eligibility.Any(e => !_customerEligibilities.Contains(e)))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Eligibility));

        if (change.Code != null && (change.CustomCodeCount is null or < 1 || change.CodeExpiryDate == null))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.CustomCodeCount));

        if ((change.CodeExpiryDate ?? _input.Today) < _input.Today)
            return Fail(MessageKey.MonetizationStartDateInPast, nameof(change.CodeExpiryDate));

        if (change.OneTimeCodeCount is < 0 or > 25000)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.OneTimeCodeCount));

        if (change.OneTimeCodeCount > 0)
            Warn(MessageKey.MonetizationOneTimeCodesNotStored, change.OneTimeCodeCount);

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.OfferEligibility ??= "STACK_WITH_INTRO_OFFERS";

        Offer(product.ProductId, field, $"{MonetizationContent.OfferText(change.Mode, change.Duration, change.Periods)} {string.Join("/", change.Eligibility)}");
        Call("POST", "/v1/subscriptionOfferCodes", change.ReferenceName);

        if (change.Code != null)
            Call("POST", "/v1/subscriptionOfferCodeCustomCodes", change.Code);

        if (change.OneTimeCodeCount > 0)
            Call("POST", "/v1/subscriptionOfferCodeOneTimeUseCodes", $"{change.OneTimeCodeCount} codes");

        _changes.Operations.Add(op);

        return true;
    }

    private bool WinBackOffer(MonetizationChangeDto change)
    {
        if (!OfferProduct(change, out var product))
            return false;

        if (!OfferIdentity(change, product, OfferKind.WinBack) || !OfferPrices(change))
            return false;

        if (change.PaidMonths is null or < 1 or > 60 || change.SinceMinMonths is null or < 0 || change.SinceMaxMonths < change.SinceMinMonths)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.PaidMonths));

        if (change.StartDate == null || change.StartDate <= _input.Today)
            return Fail(MessageKey.MonetizationStartDateInPast, nameof(change.StartDate));

        if (change.EndDate != null && change.EndDate < change.StartDate.Value.AddDays(3))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.EndDate));

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.Priority ??= "NORMAL";

        Offer(product.ProductId, $"{OfferKind.WinBack}:{change.Code}", $"{change.ReferenceName}: {MonetizationContent.OfferText(change.Mode, change.Duration, change.Periods)}");
        Call("POST", "/v1/winBackOffers", $"{product.ProductId} {change.Code}");
        _changes.Operations.Add(op);

        return true;
    }

    private bool OfferProduct(MonetizationChangeDto change, out StoreProduct product)
    {
        product = Snapshot.Product(change.ProductId);

        if (product == null || product.Kind != StoreProductKind.Subscription)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        return ValidOffer(change, product);
    }

    private bool ValidOffer(MonetizationChangeDto change, StoreProduct product)
    {
        if (change.Mode == null || change.Duration == null || !MonetizationRules.IsValidOffer(product.Duration, change.Mode.Value, change.Duration, change.Periods ?? 1))
            return Fail(MessageKey.MonetizationOfferInvalid, nameof(change.Duration));

        change.Periods ??= 1;

        return true;
    }

    private bool OfferIdentity(MonetizationChangeDto change, StoreProduct product, OfferKind kind)
    {
        if (string.IsNullOrWhiteSpace(change.ReferenceName))
            return Fail(MessageKey.MonetizationReferenceNameRequired, nameof(change.ReferenceName));

        if (!MonetizationRules.IsValidProductId(StoreKind.Apple, change.Code))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Code));

        Base(MonetizationKeys.Offer, product.ProductId, null, $"{kind}:{change.Code}");

        if ((product.Offers ?? []).Any(o => o.Kind == kind && string.Equals(o.Code, change.Code, StringComparison.Ordinal)))
            return Fail(MessageKey.MonetizationOfferExists, change.Code);

        return true;
    }

    private bool OfferPrices(MonetizationChangeDto change)
    {
        if (change.Prices == null || change.Prices.Count == 0 || change.Prices.Any(p => string.IsNullOrWhiteSpace(p.Territory) || string.IsNullOrWhiteSpace(p.PricePointId)))
            return Fail(MessageKey.MonetizationPricePointRequired, nameof(change.Prices));

        return true;
    }

    private void Offer(string owner, string field, string after)
        => _draft.Items.Add(new PlanItem { Group = MonetizationKeys.Offer, Field = $"{owner}/{field}", After = after, Action = ChangeAction.Create });
}
