using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Storeasy.Domain.Monetization;

/// <summary>
/// Pure rules of store products: state mapping, which plans need a stronger permission and a typed confirmation (a price
/// change of a product on sale reaches paying subscribers, so it counts as a production release), product id formats and
/// the offer combinations App Store Connect accepts. No database, no store calls.
/// </summary>
public static partial class MonetizationRules
{
    /// <summary>
    /// Longest product id.
    /// </summary>
    public const int ProductIdMaxLength = StoreProduct.ProductIdMaxLength;

    /// <summary>
    /// App Store limit of a subscription or In-App Purchase display name.
    /// </summary>
    public const int DisplayNameMaxLength = 30;

    /// <summary>
    /// App Store limit of a subscription or In-App Purchase description.
    /// </summary>
    public const int DescriptionMaxLength = 45;

    /// <summary>
    /// Play limit of a subscription or one-time product title.
    /// </summary>
    public const int PlayTitleMaxLength = 55;

    /// <summary>
    /// Play limit of a description.
    /// </summary>
    public const int PlayDescriptionMaxLength = 200;

    /// <summary>
    /// Subscription periods of the App Store.
    /// </summary>
    public static IReadOnlyList<string> SubscriptionPeriods { get; } = ["ONE_WEEK", "ONE_MONTH", "TWO_MONTHS", "THREE_MONTHS", "SIX_MONTHS", "ONE_YEAR"];

    /// <summary>
    /// Offer durations of the App Store.
    /// </summary>
    public static IReadOnlyList<string> OfferDurations { get; } = ["THREE_DAYS", "ONE_WEEK", "TWO_WEEKS", "ONE_MONTH", "TWO_MONTHS", "THREE_MONTHS", "SIX_MONTHS", "ONE_YEAR"];

    private static readonly HashSet<string> _zeroDecimalCurrencies = ["JPY", "KRW", "CLP", "VND", "ISK", "PYG", "UGX", "XAF", "XOF"];

    /// <summary>
    /// Store neutral state of an App Store subscription or In-App Purchase state text.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ProductState AppleState(string state) => state switch
    {
        "MISSING_METADATA" or "WAITING_FOR_UPLOAD" or "PROCESSING_CONTENT" => ProductState.MissingMetadata,
        "READY_TO_SUBMIT" => ProductState.ReadyToSubmit,
        "WAITING_FOR_REVIEW" => ProductState.WaitingForReview,
        "IN_REVIEW" => ProductState.InReview,
        "DEVELOPER_ACTION_NEEDED" or "PENDING_BINARY_APPROVAL" => ProductState.ActionNeeded,
        "APPROVED" => ProductState.Approved,
        "REJECTED" => ProductState.Rejected,
        "DEVELOPER_REMOVED_FROM_SALE" or "REMOVED_FROM_SALE" => ProductState.Removed,
        _ => ProductState.MissingMetadata,
    };

    /// <summary>
    /// Store neutral state of a Play base plan, offer or purchase option state text.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static ProductState GoogleState(string state) => state switch
    {
        "ACTIVE" => ProductState.Active,
        "INACTIVE" or "INACTIVE_PUBLISHED" => ProductState.Inactive,
        _ => ProductState.Draft,
    };

    /// <summary>
    /// State of a Play product from the states of its base plans or purchase options: active when one is active,
    /// inactive when one was ever published, otherwise draft.
    /// </summary>
    /// <param name="planStates"></param>
    /// <returns></returns>
    public static ProductState GoogleProductState(IEnumerable<ProductState> planStates)
    {
        var states = planStates.ToList();

        if (states.Contains(ProductState.Active))
            return ProductState.Active;

        return states.Contains(ProductState.Inactive) ? ProductState.Inactive : ProductState.Draft;
    }

    /// <summary>
    /// Whether a product in <paramref name="state"/> is on sale (its buyers pay the prices it has).
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool IsOnSale(ProductState state) => state is ProductState.Approved or ProductState.Active;

    /// <summary>
    /// Whether a product in <paramref name="state"/> was ever approved or published (then it is not the app's first).
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool WasEverOnSale(ProductState state) => state is ProductState.Approved or ProductState.Active or ProductState.Inactive or ProductState.Removed;

    /// <summary>
    /// Permission to approve a price change: <see cref="OrgPermission.ReleaseProduction"/> for a product on sale,
    /// otherwise <see cref="OrgPermission.EditContent"/>.
    /// </summary>
    /// <param name="onSale"></param>
    /// <returns></returns>
    public static OrgPermission PricePermission(bool onSale) => onSale ? OrgPermission.ReleaseProduction : OrgPermission.EditContent;

    /// <summary>
    /// The stronger of two permissions (a plan needs the highest permission of its items).
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static OrgPermission Highest(OrgPermission left, OrgPermission right) => Rank(left) >= Rank(right) ? left : right;

    /// <summary>
    /// Text the approver types for a price change that reaches existing subscribers, e.g. "Kumbara com.x.monthly".
    /// </summary>
    /// <param name="appName"></param>
    /// <param name="productId"></param>
    /// <returns></returns>
    public static string PriceChangeConfirmText(string appName, string productId) => ChangePlanRules.ConfirmationText(appName, productId);

    /// <summary>
    /// Whether <paramref name="productId"/> is a valid product id for <paramref name="store"/>: App Store letters, digits,
    /// periods and underscores; Play lower case letters, digits, periods and underscores, starting with a letter or digit.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="productId"></param>
    /// <returns></returns>
    public static bool IsValidProductId(StoreKind store, string productId)
    {
        if (string.IsNullOrEmpty(productId) || productId.Length > ProductIdMaxLength)
            return false;

        return store == StoreKind.Google ? PlayProductId().IsMatch(productId) : AppleProductId().IsMatch(productId);
    }

    /// <summary>
    /// Whether App Store Connect accepts an introductory offer of <paramref name="mode"/> lasting
    /// <paramref name="periods"/> x <paramref name="duration"/> on a subscription of <paramref name="subscriptionPeriod"/>:
    /// a free trial or pay up front runs once for one of the offer durations; pay as you go repeats the subscription
    /// period, at most a year in total.
    /// </summary>
    /// <param name="subscriptionPeriod"></param>
    /// <param name="mode"></param>
    /// <param name="duration"></param>
    /// <param name="periods"></param>
    /// <returns></returns>
    public static bool IsValidOffer(string subscriptionPeriod, OfferMode mode, string duration, int periods) => mode switch
    {
        OfferMode.FreeTrial => periods == 1 && OfferDurations.Contains(duration),
        OfferMode.PayUpFront => periods == 1 && duration is "ONE_MONTH" or "TWO_MONTHS" or "THREE_MONTHS" or "SIX_MONTHS" or "ONE_YEAR",
        OfferMode.PayAsYouGo => duration == subscriptionPeriod && periods >= 1 && periods <= MaxPayAsYouGoPeriods(subscriptionPeriod),
        _ => false,
    };

    /// <summary>
    /// App Store offer mode text of <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static string AppleOfferMode(OfferMode mode) => mode switch
    {
        OfferMode.FreeTrial => "FREE_TRIAL",
        OfferMode.PayUpFront => "PAY_UP_FRONT",
        _ => "PAY_AS_YOU_GO",
    };

    /// <summary>
    /// Offer mode of an App Store offer mode text.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    public static OfferMode? OfferModeOf(string mode) => mode switch
    {
        "FREE_TRIAL" => OfferMode.FreeTrial,
        "PAY_UP_FRONT" => OfferMode.PayUpFront,
        "PAY_AS_YOU_GO" => OfferMode.PayAsYouGo,
        _ => null,
    };

    /// <summary>
    /// Product kind of an App Store In-App Purchase type text.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static StoreProductKind IapKind(string type) => type switch
    {
        "CONSUMABLE" => StoreProductKind.Consumable,
        "NON_RENEWING_SUBSCRIPTION" => StoreProductKind.NonRenewingSubscription,
        _ => StoreProductKind.NonConsumable,
    };

    /// <summary>
    /// App Store In-App Purchase type text of a product kind.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static string IapType(StoreProductKind kind) => kind switch
    {
        StoreProductKind.Consumable => "CONSUMABLE",
        StoreProductKind.NonRenewingSubscription => "NON_RENEWING_SUBSCRIPTION",
        _ => "NON_CONSUMABLE",
    };

    /// <summary>
    /// Whether <paramref name="amount"/> is a positive price with the decimals <paramref name="currency"/> allows (no
    /// decimals for JPY, KRW and the like, at most two otherwise).
    /// </summary>
    /// <param name="currency"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static bool IsValidAmount(string currency, string amount)
    {
        if (!TryParseAmount(amount, out var value) || value <= 0)
            return false;

        var decimals = value.Scale;

        return _zeroDecimalCurrencies.Contains(currency?.ToUpperInvariant() ?? string.Empty) ? decimals == 0 : decimals <= 2;
    }

    /// <summary>
    /// Parses an amount written with a period as decimal separator.
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool TryParseAmount(string amount, out decimal value)
        => decimal.TryParse(amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Whether <paramref name="after"/> is a higher amount than <paramref name="before"/> (false when either is not a
    /// number).
    /// </summary>
    /// <param name="before"></param>
    /// <param name="after"></param>
    /// <returns></returns>
    public static bool IsIncrease(string before, string after)
        => TryParseAmount(before, out var old) && TryParseAmount(after, out var value) && value > old;

    /// <summary>
    /// Audit action of a monetization plan item of content group <paramref name="group"/>.
    /// </summary>
    /// <param name="group"></param>
    /// <returns></returns>
    public static AuditAction AuditActionOf(string group) => group switch
    {
        MonetizationKeys.Price => AuditAction.PriceScheduled,
        MonetizationKeys.Offer => AuditAction.OfferChanged,
        MonetizationKeys.Settings => AuditAction.PurchaseSettingsChanged,
        MonetizationKeys.Iap => AuditAction.IapChanged,
        _ => AuditAction.SubscriptionChanged,
    };

    private static int MaxPayAsYouGoPeriods(string subscriptionPeriod) => subscriptionPeriod switch
    {
        "ONE_WEEK" or "ONE_MONTH" => 12,
        "TWO_MONTHS" => 6,
        "THREE_MONTHS" => 4,
        "SIX_MONTHS" => 2,
        "ONE_YEAR" => 1,
        _ => 0,
    };

    private static int Rank(OrgPermission permission) => permission switch
    {
        OrgPermission.View => 0,
        OrgPermission.EditContent => 1,
        OrgPermission.ReleaseTest => 2,
        OrgPermission.ManageWebhooks => 3,
        OrgPermission.SubmitForReview => 4,
        OrgPermission.ReleaseProduction => 5,
        _ => 6,
    };

    [GeneratedRegex("^[A-Za-z0-9._]+$")]
    private static partial Regex AppleProductId();

    [GeneratedRegex("^[a-z0-9][a-z0-9._]*$")]
    private static partial Regex PlayProductId();
}
