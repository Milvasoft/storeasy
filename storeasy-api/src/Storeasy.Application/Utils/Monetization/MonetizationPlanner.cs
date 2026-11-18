using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;
using Storeasy.Domain.Monetization;
using System.Globalization;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// Turns requested monetization changes into a change plan, purely: every change is checked against the store's
/// current products (<see cref="MonetizationSnapshot"/>), becomes field level items (before and after), the store calls
/// the apply job will make, warnings, and an operation completed with the store's ids. The plan needs the highest
/// permission of its items: a price change of a product on sale needs <see cref="OrgPermission.ReleaseProduction"/> and,
/// when it reaches existing subscribers, a typed confirmation. storeasy never computes a price: App Store prices are
/// price points (equalized by the store), Play prices are what the member entered or Play converted.
/// </summary>
public sealed partial class MonetizationPlanner
{
    private readonly MonetizationPlanInput _input;
    private readonly IMilvaLocalizer _l;
    private readonly PlanDraft _draft;
    private readonly MonetizationChangeSet _changes = new();
    private readonly Dictionary<string, StoreProductKind> _createdProducts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _createdGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _createdPlans = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

    private OrgPermission _permission;
    private string _confirmText;
    private string _errorKey;
    private string _errorDetail;

    private MonetizationPlanner(MonetizationPlanInput input)
    {
        _input = input;
        _l = input.Localizer;
        _permission = input.Kind == ChangePlanKind.ServerNotifications ? OrgPermission.ManageWebhooks : OrgPermission.EditContent;
        _draft = new PlanDraft { Kind = input.Kind };
    }

    private MonetizationSnapshot Snapshot => _input.Snapshot;

    private StoreKind Store => _input.App.Store;

    /// <summary>
    /// Plans <see cref="MonetizationPlanInput.Changes"/>; the first blocking problem stops planning.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static MonetizationPlanResult Build(MonetizationPlanInput input)
    {
        var planner = new MonetizationPlanner(input);

        if (input.Changes == null || input.Changes.Count == 0)
            return new MonetizationPlanResult(null, MessageKey.PlanNoChanges);

        foreach (var change in input.Changes)
        {
            if (!planner.Plan(change))
                return new MonetizationPlanResult(null, planner._errorKey, planner._errorDetail);
        }

        return new MonetizationPlanResult(planner.Finish());
    }

    /// <summary>
    /// The change kinds a plan of <paramref name="kind"/> on <paramref name="store"/> accepts.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="store"></param>
    /// <param name="change"></param>
    /// <returns></returns>
    public static bool Accepts(ChangePlanKind kind, StoreKind store, MonetizationChangeKind change) => kind switch
    {
        ChangePlanKind.ServerNotifications => change == MonetizationChangeKind.ServerNotifications && store == StoreKind.Apple,
        ChangePlanKind.SandboxTesters => change == MonetizationChangeKind.SandboxTester && store == StoreKind.Apple,
        ChangePlanKind.Monetization => change is not MonetizationChangeKind.ServerNotifications and not MonetizationChangeKind.SandboxTester
                                       && (store == StoreKind.Google) == IsPlayChange(change),
        _ => false,
    };

    /// <summary>
    /// Whether <paramref name="change"/> is a Google Play change.
    /// </summary>
    /// <param name="change"></param>
    /// <returns></returns>
    public static bool IsPlayChange(MonetizationChangeKind change) => change is MonetizationChangeKind.PlaySubscription
                                                                               or MonetizationChangeKind.BasePlan
                                                                               or MonetizationChangeKind.BasePlanState
                                                                               or MonetizationChangeKind.BasePlanPrices
                                                                               or MonetizationChangeKind.PlayOffer
                                                                               or MonetizationChangeKind.PlayOfferState
                                                                               or MonetizationChangeKind.OneTimeProduct;

    /// <summary>
    /// What a plan of <paramref name="changes"/> must read: the parts and the product ids (null: every product).
    /// </summary>
    /// <param name="changes"></param>
    /// <returns></returns>
    public static (MonetizationParts Parts, HashSet<string> ProductIds) ScopeOf(IEnumerable<MonetizationChangeDto> changes)
    {
        var parts = MonetizationParts.Products | MonetizationParts.Localizations | MonetizationParts.Review;
        var productIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var change in changes)
        {
            parts |= change.Kind switch
            {
                MonetizationChangeKind.BasePrice or MonetizationChangeKind.TerritoryPrice or MonetizationChangeKind.BasePlanPrices => MonetizationParts.Prices,
                MonetizationChangeKind.Availability or MonetizationChangeKind.IntroductoryOffer => MonetizationParts.Availability | MonetizationParts.Offers,
                MonetizationChangeKind.PromotionalOffer or MonetizationChangeKind.OfferCode or MonetizationChangeKind.WinBackOffer => MonetizationParts.Offers,
                MonetizationChangeKind.PlayOffer or MonetizationChangeKind.PlayOfferState => MonetizationParts.Offers | MonetizationParts.Prices,
                MonetizationChangeKind.OneTimeProduct or MonetizationChangeKind.BasePlan => MonetizationParts.Prices,
                _ => 0,
            };

            if (!string.IsNullOrEmpty(change.ProductId))
                productIds.Add(change.ProductId);
        }

        return (parts, productIds);
    }

    private bool Plan(MonetizationChangeDto change)
    {
        if (!Accepts(_input.Kind, Store, change.Kind))
            return Fail(MessageKey.MonetizationChangeUnsupported, change.Kind.ToString());

        return change.Kind switch
        {
            MonetizationChangeKind.CreateGroup => CreateGroup(change),
            MonetizationChangeKind.UpdateGroup => UpdateGroup(change),
            MonetizationChangeKind.GroupLocalization => GroupLocalization(change),
            MonetizationChangeKind.CreateSubscription => CreateSubscription(change),
            MonetizationChangeKind.CreateIap => CreateIap(change),
            MonetizationChangeKind.UpdateProduct => UpdateProduct(change),
            MonetizationChangeKind.ProductLocalization => ProductLocalization(change),
            MonetizationChangeKind.Level => Level(change),
            MonetizationChangeKind.BasePrice => BasePrice(change),
            MonetizationChangeKind.TerritoryPrice => TerritoryPrice(change),
            MonetizationChangeKind.Availability => Availability(change),
            MonetizationChangeKind.IntroductoryOffer => IntroductoryOffer(change),
            MonetizationChangeKind.PromotionalOffer => PromotionalOffer(change),
            MonetizationChangeKind.OfferCode => OfferCode(change),
            MonetizationChangeKind.WinBackOffer => WinBackOffer(change),
            MonetizationChangeKind.ReviewScreenshot => ReviewScreenshot(change),
            MonetizationChangeKind.PlaySubscription => PlaySubscription(change),
            MonetizationChangeKind.BasePlan => BasePlan(change),
            MonetizationChangeKind.BasePlanState => BasePlanState(change),
            MonetizationChangeKind.BasePlanPrices => BasePlanPrices(change),
            MonetizationChangeKind.PlayOffer => PlayOffer(change),
            MonetizationChangeKind.PlayOfferState => PlayOfferState(change),
            MonetizationChangeKind.OneTimeProduct => OneTimeProduct(change),
            MonetizationChangeKind.ServerNotifications => ServerNotifications(change),
            MonetizationChangeKind.SandboxTester => SandboxTester(change),
            _ => Fail(MessageKey.MonetizationChangeUnsupported, change.Kind.ToString()),
        };
    }

    private PlanDraft Finish()
    {
        _draft.Title = Trim($"{Title()}: {_draft.Items.Count} changes", 200);
        _draft.RequiredPermission = _permission;
        _draft.ConfirmText = _confirmText;
        _draft.CustomChanges = _changes;
        _draft.BaseKeys = [.. _draft.BaseKeys.Distinct(StringComparer.Ordinal)];

        foreach (var item in _draft.Items.Where(i => i.Group == MonetizationKeys.Product && IsIap(MonetizationContent.ProductIdOf(MonetizationKeys.OwnerOf(ContentKey.Of(i.Group, i.Locale, i.Field))))))
            item.Group = MonetizationKeys.Iap;

        return _draft;
    }

    private bool IsIap(string productId)
    {
        var kind = Snapshot.Product(productId)?.Kind ?? _createdProducts.GetValueOrDefault(productId ?? string.Empty, StoreProductKind.Subscription);

        return kind != StoreProductKind.Subscription;
    }

    private string Title() => _input.Kind switch
    {
        ChangePlanKind.ServerNotifications => "Server notifications",
        ChangePlanKind.SandboxTesters => "Sandbox testers",
        _ => Store == StoreKind.Google ? "Play products" : "Subscriptions and In-App Purchases",
    };

    #region Groups

    private bool CreateGroup(MonetizationChangeDto change)
    {
        var name = change.GroupReferenceName ?? change.ReferenceName;

        if (string.IsNullOrWhiteSpace(name) || name.Length > ProductGroup.ReferenceNameMaxLength)
            return Fail(MessageKey.MonetizationReferenceNameRequired, nameof(change.ReferenceName));

        if (Snapshot.Group(name, null) != null || !_createdGroups.Add(name))
            return Fail(MessageKey.MonetizationGroupExists, name);

        var op = change.Copy();
        op.GroupReferenceName = name;

        Item(MonetizationKeys.Group, name, null, "referenceName", null, name);

        foreach (var localization in change.Localizations ?? [])
        {
            if (string.IsNullOrWhiteSpace(localization.Locale) || string.IsNullOrWhiteSpace(localization.Name))
                return Fail(MessageKey.MonetizationLocalizationInvalid, nameof(change.Localizations));

            Item(MonetizationKeys.Group, name, localization.Locale, "name", null, localization.Name);
            Item(MonetizationKeys.Group, name, localization.Locale, "customAppName", null, localization.CustomAppName);
        }

        Call("POST", "/v1/subscriptionGroups", name);

        if (change.Localizations?.Count > 0)
            Call("POST", "/v2/subscriptionGroupLocalizations", $"{change.Localizations.Count} localizations");

        _changes.Operations.Add(op);

        return true;
    }

    private bool UpdateGroup(MonetizationChangeDto change)
    {
        var group = Snapshot.Group(change.GroupReferenceName, change.GroupExternalId);

        if (group == null)
            return Fail(MessageKey.MonetizationGroupNotFound, change.GroupReferenceName);

        if (string.IsNullOrWhiteSpace(change.ReferenceName) || change.ReferenceName.Length > ProductGroup.ReferenceNameMaxLength)
            return Fail(MessageKey.MonetizationReferenceNameRequired, nameof(change.ReferenceName));

        if (!Item(MonetizationKeys.Group, group.ReferenceName, null, "referenceName", group.ReferenceName, change.ReferenceName))
            return true;

        var op = change.Copy();
        op.GroupExternalId = group.ExternalId;

        Call("PATCH", $"/v1/subscriptionGroups/{group.ExternalId}", group.ReferenceName);
        _changes.Operations.Add(op);

        return true;
    }

    private bool GroupLocalization(MonetizationChangeDto change)
    {
        var name = change.GroupReferenceName;
        var group = Snapshot.Group(name, change.GroupExternalId);

        if (group == null && !_createdGroups.Contains(name ?? string.Empty))
            return Fail(MessageKey.MonetizationGroupNotFound, name);

        if (string.IsNullOrWhiteSpace(change.Locale) || string.IsNullOrWhiteSpace(change.Name))
            return Fail(MessageKey.MonetizationLocalizationInvalid, nameof(change.Locale));

        var owner = group?.ReferenceName ?? name;
        var current = group?.Localizations?.FirstOrDefault(l => l.Locale == change.Locale);
        var changed = Item(MonetizationKeys.Group, owner, change.Locale, "name", current?.Name, change.Name);

        changed |= Item(MonetizationKeys.Group, owner, change.Locale, "customAppName", current?.CustomAppName, change.CustomAppName);

        if (!changed)
            return true;

        var op = change.Copy();
        op.GroupExternalId = group?.ExternalId;
        op.GroupReferenceName = owner;

        Call(current == null ? "POST" : "PATCH", "/v2/subscriptionGroupLocalizations", $"{owner} {change.Locale}");
        _changes.Operations.Add(op);

        return true;
    }

    #endregion

    #region Products

    private bool CreateSubscription(MonetizationChangeDto change)
    {
        if (!NewProduct(change))
            return false;

        var groupName = change.GroupReferenceName;
        var group = Snapshot.Group(groupName, change.GroupExternalId);

        if (group == null && !_createdGroups.Contains(groupName ?? string.Empty))
            return Fail(MessageKey.MonetizationGroupNotFound, groupName);

        if (!MonetizationRules.SubscriptionPeriods.Contains(change.Duration ?? string.Empty))
            return Fail(MessageKey.MonetizationDurationInvalid, nameof(change.Duration));

        var op = change.Copy();
        op.GroupExternalId = group?.ExternalId;
        op.GroupReferenceName = group?.ReferenceName ?? groupName;

        _createdProducts[change.ProductId] = StoreProductKind.Subscription;

        Item(MonetizationKeys.Product, change.ProductId, null, "productId", null, change.ProductId);
        Item(MonetizationKeys.Product, change.ProductId, null, "name", null, change.ReferenceName);
        Item(MonetizationKeys.Product, change.ProductId, null, "duration", null, change.Duration);
        Item(MonetizationKeys.Product, change.ProductId, null, "level", null, change.Level?.ToString(CultureInfo.InvariantCulture));
        Item(MonetizationKeys.Product, change.ProductId, null, "familySharable", null, change.FamilySharable == true ? "True" : null);

        if (change.FamilySharable == true)
            Warn(MessageKey.MonetizationFamilySharingIrreversible, change.ProductId);

        Call("POST", "/v1/subscriptions", change.ProductId);
        _changes.Operations.Add(op);

        return true;
    }

    private bool CreateIap(MonetizationChangeDto change)
    {
        if (change.ProductKind is not (StoreProductKind.Consumable or StoreProductKind.NonConsumable or StoreProductKind.NonRenewingSubscription))
            return Fail(MessageKey.MonetizationChangeUnsupported, nameof(change.ProductKind));

        if (!NewProduct(change))
            return false;

        _createdProducts[change.ProductId] = change.ProductKind.Value;

        Item(MonetizationKeys.Product, change.ProductId, null, "productId", null, change.ProductId);
        Item(MonetizationKeys.Product, change.ProductId, null, "name", null, change.ReferenceName);
        Item(MonetizationKeys.Product, change.ProductId, null, "type", null, change.ProductKind.ToString());

        Call("POST", "/v2/inAppPurchases", change.ProductId);
        _changes.Operations.Add(change.Copy());

        return true;
    }

    private bool NewProduct(MonetizationChangeDto change)
    {
        if (!MonetizationRules.IsValidProductId(Store, change.ProductId))
            return Fail(MessageKey.MonetizationProductIdInvalid, nameof(change.ProductId));

        if (Snapshot.Product(change.ProductId) != null || _createdProducts.ContainsKey(change.ProductId))
            return Fail(MessageKey.MonetizationProductIdTaken, change.ProductId);

        if (string.IsNullOrWhiteSpace(change.ReferenceName) || change.ReferenceName.Length > StoreProduct.ReferenceNameMaxLength)
            return Fail(MessageKey.MonetizationReferenceNameRequired, nameof(change.ReferenceName));

        Base(MonetizationKeys.Product, change.ProductId, null, "productId");

        return true;
    }

    private bool UpdateProduct(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (change.ReferenceName != null && (string.IsNullOrWhiteSpace(change.ReferenceName) || change.ReferenceName.Length > StoreProduct.ReferenceNameMaxLength))
            return Fail(MessageKey.MonetizationReferenceNameRequired, nameof(change.ReferenceName));

        if (product.FamilySharable == true && change.FamilySharable == false)
            return Fail(MessageKey.MonetizationFamilySharingLocked, change.ProductId);

        if (change.ReviewNote?.Length > StoreProduct.ReviewNoteMaxLength)
            return Fail(MessageKey.MonetizationTextTooLong, nameof(change.ReviewNote));

        if (change.MultiSeatStatus != null && (product.Kind != StoreProductKind.Subscription || change.MultiSeatStatus is not ("ENABLED" or "DISABLED")))
            return Fail(MessageKey.MonetizationChangeUnsupported, nameof(change.MultiSeatStatus));

        var owner = product.ProductId;
        var changed = change.ReferenceName != null && Item(MonetizationKeys.Product, owner, null, "name", product.ReferenceName, change.ReferenceName);

        if (change.FamilySharable != null && Item(MonetizationKeys.Product, owner, null, "familySharable", product.FamilySharable?.ToString(), change.FamilySharable.ToString()))
        {
            changed = true;
            Warn(MessageKey.MonetizationFamilySharingIrreversible, owner);
        }

        if (change.ReviewNote != null)
            changed |= Item(MonetizationKeys.Product, owner, null, "reviewNote", product.ReviewNote, change.ReviewNote);

        if (change.MultiSeatStatus != null)
            changed |= Item(MonetizationKeys.Product, owner, null, "multiSeatStatus", product.MultiSeatStatus, change.MultiSeatStatus);

        if (!changed)
            return true;

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.ProductKind = product.Kind;

        Call("PATCH", product.Kind == StoreProductKind.Subscription ? $"/v1/subscriptions/{product.ExternalId}" : $"/v2/inAppPurchases/{product.ExternalId}", owner);
        _changes.Operations.Add(op);

        return true;
    }

    private bool ProductLocalization(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);
        var kind = product?.Kind ?? (_createdProducts.TryGetValue(change.ProductId ?? string.Empty, out var created) ? created : (StoreProductKind?)null);

        if (kind == null)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (string.IsNullOrWhiteSpace(change.Locale) || string.IsNullOrWhiteSpace(change.Name))
            return Fail(MessageKey.MonetizationLocalizationInvalid, nameof(change.Locale));

        if (ContentDiff.CharCount(change.Name) > MonetizationRules.DisplayNameMaxLength)
            return Fail(MessageKey.MonetizationTextTooLong, nameof(change.Name));

        if (ContentDiff.CharCount(change.Description) > MonetizationRules.DescriptionMaxLength)
            return Fail(MessageKey.MonetizationTextTooLong, nameof(change.Description));

        var current = product?.Localizations?.FirstOrDefault(l => l.Locale == change.Locale);
        var changed = Item(MonetizationKeys.Product, change.ProductId, change.Locale, "name", current?.Name, change.Name);

        changed |= Item(MonetizationKeys.Product, change.ProductId, change.Locale, "description", current?.Description, change.Description);

        if (!changed)
            return true;

        var op = change.Copy();
        op.ProductExternalId = product?.ExternalId;
        op.ProductKind = kind;

        Call(current == null ? "POST" : "PATCH", kind == StoreProductKind.Subscription ? "/v2/subscriptionLocalizations" : "/v2/inAppPurchaseLocalizations", $"{change.ProductId} {change.Locale}");
        _changes.Operations.Add(op);

        return true;
    }

    private bool Level(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null || product.Kind != StoreProductKind.Subscription)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (change.Level is null or < 1)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Level));

        if (!Item(MonetizationKeys.Product, product.ProductId, null, "level", product.Level?.ToString(CultureInfo.InvariantCulture), change.Level.Value.ToString(CultureInfo.InvariantCulture)))
            return true;

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;

        Call("PATCH", $"/v1/subscriptions/{product.ExternalId}", $"{product.ProductId} groupLevel");
        _changes.Operations.Add(op);

        return true;
    }

    private bool Availability(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);

        if (product == null || Store != StoreKind.Apple)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        if (change.Territories == null || change.Territories.Count == 0)
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.Territories));

        var territories = change.Territories.Select(t => t.Trim().ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal).ToList();
        var current = product.Availability?.Territories?.Order(StringComparer.Ordinal).ToList();
        var changed = Item(MonetizationKeys.Availability, product.ProductId, null, "territories", current == null ? null : string.Join(",", current), string.Join(",", territories));

        if (change.AvailableInNewTerritories != null)
            changed |= Item(MonetizationKeys.Availability, product.ProductId, null, "availableInNewTerritories", product.Availability?.AvailableInNewTerritories?.ToString(), change.AvailableInNewTerritories.ToString());

        if (!changed)
            return true;

        var op = change.Copy();
        op.ProductExternalId = product.ExternalId;
        op.ProductKind = product.Kind;
        op.Territories = territories;
        op.AvailableInNewTerritories ??= product.Availability?.AvailableInNewTerritories ?? true;

        Call("POST", product.Kind == StoreProductKind.Subscription ? "/v1/subscriptionAvailabilities" : "/v1/inAppPurchaseAvailabilities", $"{territories.Count} territories");
        _changes.Operations.Add(op);

        return true;
    }

    private bool ReviewScreenshot(MonetizationChangeDto change)
    {
        var product = Snapshot.Product(change.ProductId);
        var kind = product?.Kind ?? (_createdProducts.TryGetValue(change.ProductId ?? string.Empty, out var created) ? created : (StoreProductKind?)null);

        if (kind == null)
            return Fail(MessageKey.MonetizationProductNotFound, change.ProductId);

        var asset = _input.Assets?.FirstOrDefault(a => a.AssetId == change.AssetId);

        if (asset == null)
            return Fail(MessageKey.AssetNotFound, nameof(change.AssetId));

        if (asset.Image == null || Math.Min(asset.Image.Width, asset.Image.Height) < 640 || Math.Max(asset.Image.Width, asset.Image.Height) < 920)
            return Fail(MessageKey.MonetizationScreenshotInvalid, asset.FileName);

        _draft.Files.Add(new PlanFile { AssetId = asset.AssetId, FileName = asset.FileName, Width = asset.Image.Width, Height = asset.Image.Height, Md5 = asset.Md5, Result = "Accepted" });

        if (!Item(MonetizationKeys.Product, change.ProductId, null, "reviewScreenshot", product?.ReviewScreenshot?.Checksum ?? product?.ReviewScreenshot?.FileName, asset.Md5))
            return true;

        var op = change.Copy();
        op.ProductExternalId = product?.ExternalId;
        op.ProductKind = kind;
        op.ObjectKey = asset.ObjectKey;
        op.FileName = asset.FileName;
        op.Md5 = asset.Md5;
        op.Size = asset.Size;

        Call("POST", kind == StoreProductKind.Subscription ? "/v1/subscriptionAppStoreReviewScreenshots" : "/v1/inAppPurchaseAppStoreReviewScreenshots", asset.FileName);
        _changes.Operations.Add(op);

        return true;
    }

    #endregion

    #region Settings

    private bool ServerNotifications(MonetizationChangeDto change)
    {
        if (!IsHttps(change.Url) || !IsHttps(change.SandboxUrl))
            return Fail(MessageKey.MonetizationUrlInvalid, change.Url == null || IsHttps(change.Url) ? nameof(change.SandboxUrl) : nameof(change.Url));

        var version = change.NotificationVersion ?? "V2";

        if (version is not ("V1" or "V2"))
            return Fail(MessageKey.MonetizationValueInvalid, nameof(change.NotificationVersion));

        var owner = MonetizationContent.AppOwner;
        var changed = false;

        if (change.Url != null)
        {
            changed |= Item(MonetizationKeys.Settings, owner, null, "notificationUrl", Snapshot.NotificationUrl, change.Url);
            changed |= Item(MonetizationKeys.Settings, owner, null, "notificationVersion", Snapshot.NotificationVersion, version);
        }

        if (change.SandboxUrl != null)
        {
            changed |= Item(MonetizationKeys.Settings, owner, null, "sandboxNotificationUrl", Snapshot.SandboxNotificationUrl, change.SandboxUrl);
            changed |= Item(MonetizationKeys.Settings, owner, null, "sandboxNotificationVersion", Snapshot.SandboxNotificationVersion, version);
        }

        if (!changed)
            return true;

        var op = change.Copy();
        op.NotificationVersion = version;

        Call("PATCH", $"/v1/apps/{_input.App.ExternalId}", "subscriptionStatusUrl");
        _changes.Operations.Add(op);

        return true;
    }

    private bool SandboxTester(MonetizationChangeDto change)
    {
        var tester = _input.Testers?.FirstOrDefault(t => t.Id == change.TesterId);

        if (tester == null)
            return Fail(MessageKey.MonetizationTesterNotFound, change.TesterId);

        var owner = $"sandbox:{tester.AccountName ?? tester.Id}";
        var changed = false;

        if (change.Territory != null)
            changed |= Item(MonetizationKeys.Settings, owner, null, "territory", tester.Territory, change.Territory.Trim().ToUpperInvariant(), fingerprint: false);

        if (change.InterruptPurchases != null)
            changed |= Item(MonetizationKeys.Settings, owner, null, "interruptPurchases", tester.InterruptPurchases?.ToString(), change.InterruptPurchases.ToString(), fingerprint: false);

        if (change.RenewalRate != null)
            changed |= Item(MonetizationKeys.Settings, owner, null, "subscriptionRenewalRate", tester.SubscriptionRenewalRate, change.RenewalRate, fingerprint: false);

        if (change.ClearPurchaseHistory == true)
        {
            _draft.Items.Add(new PlanItem { Group = MonetizationKeys.Settings, Field = $"{owner}/purchaseHistory", Before = null, After = null, Action = ChangeAction.Delete });
            Call("POST", "/v2/sandboxTestersClearPurchaseHistoryRequest", owner);
            changed = true;
        }

        if (!changed)
            return true;

        var op = change.Copy();
        op.Territory = change.Territory?.Trim().ToUpperInvariant();

        Call("PATCH", $"/v2/sandboxTesters/{tester.Id}", owner);
        _changes.Operations.Add(op);

        return true;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Adds an item when the value changes and fingerprints the field. Returns whether it changes.
    /// </summary>
    private bool Item(string group, string owner, string locale, string field, string before, string after, bool fingerprint = true)
    {
        if (fingerprint)
            Base(group, owner, locale, field);

        if (ContentDiff.Same(before, after))
            return false;

        _draft.Items.Add(new PlanItem
        {
            Group = group,
            Locale = locale,
            Field = $"{owner}/{field}",
            Before = ContentSnapshots.Short(Empty(before)),
            After = ContentSnapshots.Short(Empty(after)),
            Action = string.IsNullOrEmpty(before) ? ChangeAction.Create : string.IsNullOrEmpty(after) ? ChangeAction.Delete : ChangeAction.Update,
        });

        return true;
    }

    private void Base(string group, string owner, string locale, string field) => _draft.BaseKeys.Add(MonetizationKeys.Of(group, owner, locale, field));

    private void Call(string method, string path, string target) => _draft.Calls.Add(new PlanCall { Method = method, Path = path, Target = target });

    private void Warn(string key, params object[] arguments)
    {
        var message = (string)_l[key, arguments];

        if (_warned.Add($"{key}|{message}"))
            _draft.Warnings.Add(new PlanWarning { Key = key, Message = message, Field = arguments.Length > 0 ? arguments[0]?.ToString() : null });
    }

    private void Require(OrgPermission permission) => _permission = MonetizationRules.Highest(_permission, permission);

    private bool Fail(string key, string detail = null)
    {
        _errorKey = key;
        _errorDetail = detail;

        return false;
    }

    private static bool IsHttps(string url)
        => url == null || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);

    private static string Empty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    #endregion
}
