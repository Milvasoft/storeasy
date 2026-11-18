using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Monetization;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Readiness items of the products an app sells (docs/urun-plani.md 11.1 and 11.2): on the App Store every subscription
/// and In-App Purchase needs a review screenshot and a localization, and the app's first ones cannot be submitted on
/// their own (they go to review with the version); on Play a subscription without an active base plan cannot be bought
/// and Real-time developer notifications are an assisted step. Apps that sell nothing get no item. Read live; a failed
/// read becomes one unknown item and never fails the check.
/// </summary>
/// <param name="storeAccess"></param>
/// <param name="stepRepository"></param>
/// <param name="localizer"></param>
public sealed class MonetizationReadiness(MonetizationStoreAccess storeAccess,
                                         IStoreasyRepositoryBase<AssistedStep> stepRepository,
                                         IMilvaLocalizer localizer) : IReadinessContributor
{
    private const string _group = "Monetization";
    private const string _fixRoute = "money/subscriptions";

    private readonly MonetizationStoreAccess _storeAccess = storeAccess;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = stepRepository;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadinessItemDto>> EvaluateAsync(StoreAppContext app, StoreContentSnapshot content, CancellationToken cancellationToken)
    {
        if (app.Store is not (StoreKind.Apple or StoreKind.Google))
            return [];

        var read = await _storeAccess.ReadAsync(app, MonetizationParts.Products | MonetizationParts.Localizations | MonetizationParts.Review, null, cancellationToken);

        if (!read.IsSuccess)
            return [Item("monetization", ReadinessStatus.Unknown, MessageKey.ReadinessMonetization, _localizer[MessageKey.ReadinessMonetizationUnknown])];

        var products = read.Snapshot.Products.Where(p => p.State != ProductState.Removed).ToList();

        if (products.Count == 0)
            return [];

        return app.Store == StoreKind.Apple ? Apple(read.Snapshot, products) : await GoogleAsync(app, products, cancellationToken);
    }

    private List<ReadinessItemDto> Apple(MonetizationSnapshot snapshot, List<StoreProduct> products)
    {
        var items = new List<ReadinessItemDto>();
        var noScreenshot = products.Where(p => p.ReviewScreenshot == null).Select(p => p.ProductId).ToList();
        var noLocalization = products.Where(p => p.Localizations == null || p.Localizations.Count == 0).Select(p => p.ProductId)
                                     .Concat(snapshot.Groups.Where(g => g.Localizations == null || g.Localizations.Count == 0).Select(g => g.ReferenceName))
                                     .ToList();

        items.Add(Item("monetization.review-screenshot",
                       noScreenshot.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok,
                       MessageKey.ReadinessReviewScreenshot,
                       noScreenshot.Count > 0 ? _localizer[MessageKey.ReadinessReviewScreenshotMissing, string.Join(", ", noScreenshot)] : null,
                       "apple.review.iap-review-screenshot"));

        items.Add(Item("monetization.localizations",
                       noLocalization.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok,
                       MessageKey.ReadinessProductLocalizations,
                       noLocalization.Count > 0 ? _localizer[MessageKey.ReadinessProductLocalizationsMissing, string.Join(", ", noLocalization)] : null));

        if (!products.Any(p => MonetizationRules.WasEverOnSale(p.State)))
        {
            var first = products.Where(p => p.State is ProductState.ReadyToSubmit or ProductState.MissingMetadata).Select(p => p.ProductId).ToList();

            if (first.Count > 0)
            {
                items.Add(Item("monetization.first-with-version",
                               ReadinessStatus.Warning,
                               MessageKey.ReadinessFirstProducts,
                               _localizer[MessageKey.ReadinessFirstProductsWithVersion, string.Join(", ", first)],
                               "apple.review.iap-first-with-version"));
            }
        }

        return items;
    }

    private async Task<List<ReadinessItemDto>> GoogleAsync(StoreAppContext app, List<StoreProduct> products, CancellationToken cancellationToken)
    {
        var items = new List<ReadinessItemDto>();
        var subscriptions = products.Where(p => p.Kind == StoreProductKind.Subscription).ToList();

        if (subscriptions.Count == 0)
            return items;

        var inactive = subscriptions.Where(p => !p.Plans.Any(b => b.State == ProductState.Active)).Select(p => p.ProductId).ToList();

        items.Add(Item("monetization.base-plans",
                       inactive.Count > 0 ? ReadinessStatus.Warning : ReadinessStatus.Ok,
                       MessageKey.ReadinessPlayBasePlans,
                       inactive.Count > 0 ? _localizer[MessageKey.ReadinessPlayBasePlansInactive, string.Join(", ", inactive)] : null));

        var step = await _stepRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == app.OrganizationId && s.AppId == app.AppId && s.Store == app.Store && s.Key == MonetizationAssistedSteps.RealTimeNotifications,
                                                                projection: s => new AssistedStep { Id = s.Id, Status = s.Status },
                                                                cancellationToken: cancellationToken);

        var done = step?.Status is AssistedStepStatus.Done or AssistedStepStatus.Skipped;

        items.Add(new ReadinessItemDto
        {
            Key = $"assisted.{MonetizationAssistedSteps.RealTimeNotifications}",
            Group = "Assisted",
            Status = done ? ReadinessStatus.Ok : ReadinessStatus.Assisted,
            Title = _localizer[$"AssistedStep.{MonetizationAssistedSteps.RealTimeNotifications}.Title"],
            Message = done ? null : _localizer[MessageKey.ReadinessStepOpen],
            FixRoute = "assisted",
        });

        return items;
    }

    private ReadinessItemDto Item(string key, ReadinessStatus status, string titleKey, string message, string ruleId = null) => new()
    {
        Key = key,
        Group = _group,
        Status = status,
        RuleId = ruleId,
        Title = _localizer[titleKey],
        Message = message,
        FixRoute = _fixRoute,
    };
}
