using Storeasy.Application.Interfaces;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// App Store Connect content engine, ported from store-ops (behaviour, not style):
/// <list type="bullet">
/// <item>Version texts: the app info localization is written first; creating it can create the version localization of
/// the same locale on Apple's side, so the version localizations are listed again and a create that answers 409 becomes
/// a PATCH.</item>
/// <item>Screenshots and previews: a set is made to match the desired list by MD5 (kept, deleted, uploaded through
/// reserve, upload operations and commit with the checksum, reordered). A failed upload deletes its reservation; a set
/// creation that answers 5xx is retried and then looked up again.</item>
/// <item>Builds are attached only when VALID; submission reuses an open review submission.</item>
/// </list>
/// </summary>
/// <param name="http"></param>
/// <param name="fileStorage"></param>
public sealed partial class AppleContentGateway(StoreHttp http, IFileStorage fileStorage) : IStoreContentGateway
{
    private static readonly HashSet<string> _editableStates = ["PREPARE_FOR_SUBMISSION", "DEVELOPER_REJECTED", "REJECTED", "METADATA_REJECTED", "INVALID_BINARY"];
    private static readonly HashSet<string> _liveStates = ["READY_FOR_SALE", "READY_FOR_DISTRIBUTION"];
    private static readonly string[] _appInfoFields = ["name", "subtitle", "privacyPolicyUrl", "privacyChoicesUrl"];
    private static readonly string[] _versionLocalizationFields = ["description", "keywords", "promotionalText", "marketingUrl", "supportUrl", "whatsNew"];
    private static readonly string[] _reviewFields = ["contactFirstName", "contactLastName", "contactPhone", "contactEmail", "demoAccountName", "demoAccountRequired", "notes"];

    /// <summary>
    /// Secret key of the demo account password in <see cref="ContentChangeSet.Secrets"/>.
    /// </summary>
    public const string DemoPasswordSecret = "demoAccountPassword";

    /// <summary>
    /// Value shown instead of a password in the content model.
    /// </summary>
    public const string MaskedSecret = "********";

    private readonly StoreHttp _http = http;
    private readonly IFileStorage _fileStorage = fileStorage;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Apple;

    /// <inheritdoc/>
    public async Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var appId = Uri.EscapeDataString(target.ExternalId);
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Apple, ExternalId = target.ExternalId };

        using (var app = await api.GetAsync($"/v1/apps/{appId}?fields[apps]=name,bundleId,primaryLocale,contentRightsDeclaration", cancellationToken))
        {
            var data = JsonRead.Get(app.RootElement, "data");

            snapshot.PrimaryLocale = JsonRead.Attr(data, "primaryLocale");
            snapshot.Values[ContentKey.Of(ContentGroups.App, null, "contentRightsDeclaration")] = JsonRead.Attr(data, "contentRightsDeclaration");
        }

        var version = await ReadVersionsAsync(api, appId, target.VersionId, snapshot, cancellationToken);
        var appInfoId = parts.HasFlag(ContentParts.Texts) || parts.HasFlag(ContentParts.Categories) || parts.HasFlag(ContentParts.AgeRating)
            ? await EditableAppInfoIdAsync(api, appId, cancellationToken)
            : null;

        if (version.ValueKind == JsonValueKind.Object)
        {
            snapshot.Values[ContentKey.Of(ContentGroups.Version, null, "copyright")] = JsonRead.Attr(version, "copyright");
            snapshot.Values[ContentKey.Of(ContentGroups.Version, null, "releaseType")] = JsonRead.Attr(version, "releaseType");
            snapshot.Values[ContentKey.Of(ContentGroups.Version, null, "earliestReleaseDate")] = JsonRead.Attr(version, "earliestReleaseDate");
        }

        var versionLocalizations = new List<JsonElement>();

        if (snapshot.VersionId != null && (parts & (ContentParts.Texts | ContentParts.Screenshots | ContentParts.Previews)) != 0)
            versionLocalizations = (await api.GetAllAsync($"/v1/appStoreVersions/{snapshot.VersionId}/appStoreVersionLocalizations?limit=50", cancellationToken)).Data;

        if (parts.HasFlag(ContentParts.Texts))
            await ReadTextsAsync(api, appId, appInfoId, versionLocalizations, snapshot, cancellationToken);
        else
            snapshot.Locales = [.. versionLocalizations.Select(l => JsonRead.Attr(l, "locale")).Where(l => l != null)];

        if (parts.HasFlag(ContentParts.Categories) && appInfoId != null)
            await ReadCategoriesAsync(api, appInfoId, snapshot, cancellationToken);

        if (parts.HasFlag(ContentParts.AgeRating) && appInfoId != null)
            await ReadAgeRatingAsync(api, appInfoId, snapshot, cancellationToken);

        if (parts.HasFlag(ContentParts.Pricing))
            await ReadPricingAsync(api, appId, snapshot, cancellationToken);

        if (parts.HasFlag(ContentParts.Screenshots))
            await ReadMediaAsync(api, versionLocalizations, MediaKind.Screenshot, snapshot, cancellationToken);

        if (parts.HasFlag(ContentParts.Previews))
            await ReadMediaAsync(api, versionLocalizations, MediaKind.Preview, snapshot, cancellationToken);

        if (parts.HasFlag(ContentParts.Review) && snapshot.VersionId != null)
            await ReadReviewAsync(api, snapshot, cancellationToken);

        if (parts.HasFlag(ContentParts.Release) && snapshot.VersionId != null)
            await ReadBuildAsync(api, snapshot, cancellationToken);

        return snapshot;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
    {
        var api = Api(target);

        switch (kind)
        {
            case StoreOptionKind.Categories:
            {
                var (data, _) = await api.GetAllAsync("/v1/appCategories?filter[platforms]=IOS&limit=200", cancellationToken);

                return [.. data.Select(c => new StoreOption(JsonRead.Str(c, "id"), JsonRead.Str(c, "id")))];
            }
            case StoreOptionKind.PricePoints:
            {
                var territory = string.IsNullOrWhiteSpace(filter) ? "USA" : filter.Trim().ToUpperInvariant();
                var (data, _) = await api.GetAllAsync($"/v1/apps/{Uri.EscapeDataString(target.ExternalId)}/appPricePoints?filter[territory]={Uri.EscapeDataString(territory)}&limit=200", cancellationToken);

                return [.. data.Select(p => new StoreOption(JsonRead.Str(p, "id"), JsonRead.Attr(p, "customerPrice"), JsonRead.Attr(p, "proceeds")))];
            }
            default:
            {
                var (data, _) = await api.GetAllAsync("/v1/territories?limit=200", cancellationToken);

                return [.. data.Select(t => new StoreOption(JsonRead.Str(t, "id"), JsonRead.Str(t, "id"), JsonRead.Attr(t, "currency")))];
            }
        }
    }

    private AppStoreConnectApi Api(StoreContentTarget target)
    {
        var credentials = StoreCredentialParser.ParseApple(target.SecretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return new AppStoreConnectApi(_http, credentials);
    }

    private static async Task<JsonElement> ReadVersionsAsync(AppStoreConnectApi api, string appId, string versionId, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        var (data, included) = await api.GetAllAsync($"/v1/apps/{appId}/appStoreVersions?limit=50&include=appStoreVersionPhasedRelease,build", cancellationToken);

        var versions = data.OrderByDescending(v => JsonRead.Date(JsonRead.Get(v, "attributes"), "createdDate") ?? DateTime.MinValue).ToList();

        snapshot.HasLiveVersion = versions.Any(v => _liveStates.Contains(State(v)));
        snapshot.Versions = [.. versions.Select(v =>
        {
            var phased = included.FirstOrDefault(i => JsonRead.Str(i, "type") == "appStoreVersionPhasedReleases" && JsonRead.Str(i, "id") == JsonRead.RelId(v, "appStoreVersionPhasedRelease"));
            var buildId = JsonRead.RelId(v, "build");
            var build = included.FirstOrDefault(i => JsonRead.Str(i, "type") == "builds" && JsonRead.Str(i, "id") == buildId);

            return new StoreVersionState
            {
                Id = JsonRead.Str(v, "id"),
                VersionString = JsonRead.Attr(v, "versionString"),
                Platform = JsonRead.Attr(v, "platform"),
                State = State(v),
                ReleaseType = JsonRead.Attr(v, "releaseType"),
                EarliestReleaseDate = JsonRead.Date(JsonRead.Get(v, "attributes"), "earliestReleaseDate"),
                PhasedReleaseState = phased.ValueKind == JsonValueKind.Object ? JsonRead.Attr(phased, "phasedReleaseState") : null,
                PhasedReleaseDay = phased.ValueKind == JsonValueKind.Object ? JsonRead.Int(JsonRead.Get(phased, "attributes"), "currentDayNumber") : null,
                PhasedReleaseStartDate = phased.ValueKind == JsonValueKind.Object ? JsonRead.Date(JsonRead.Get(phased, "attributes"), "startDate") : null,
                BuildId = buildId,
                BuildNumber = build.ValueKind == JsonValueKind.Object ? JsonRead.Attr(build, "version") : null,
            };
        })];

        JsonElement chosen;

        if (versionId != null)
        {
            chosen = versions.FirstOrDefault(v => JsonRead.Str(v, "id") == versionId);
        }
        else
        {
            // The editable iOS version, else any editable one, else the newest.
            chosen = versions.FirstOrDefault(v => _editableStates.Contains(State(v)) && JsonRead.Attr(v, "platform") == "IOS");

            if (chosen.ValueKind != JsonValueKind.Object)
                chosen = versions.FirstOrDefault(v => _editableStates.Contains(State(v)));

            if (chosen.ValueKind != JsonValueKind.Object)
                chosen = versions.FirstOrDefault();
        }

        if (chosen.ValueKind != JsonValueKind.Object)
            return default;

        var state = snapshot.Versions.First(v => v.Id == JsonRead.Str(chosen, "id"));

        snapshot.VersionId = state.Id;
        snapshot.VersionString = state.VersionString;
        snapshot.VersionState = state.State;
        snapshot.Platform = state.Platform;
        snapshot.VersionEditable = _editableStates.Contains(state.State);
        snapshot.Values[ContentKey.Of(ContentGroups.Phased, null, "state")] = state.PhasedReleaseState;

        return chosen;
    }

    private static string State(JsonElement version) => JsonRead.Attr(version, "appVersionState") ?? JsonRead.Attr(version, "appStoreState");

    private static async Task<string> EditableAppInfoIdAsync(AppStoreConnectApi api, string appId, CancellationToken cancellationToken)
    {
        var (infos, _) = await api.GetAllAsync($"/v1/apps/{appId}/appInfos", cancellationToken);

        var editable = infos.FirstOrDefault(i => !_liveStates.Contains(JsonRead.Attr(i, "state") ?? JsonRead.Attr(i, "appStoreState") ?? string.Empty));

        return JsonRead.Str(editable.ValueKind == JsonValueKind.Object ? editable : infos.FirstOrDefault(), "id");
    }

    private static async Task ReadTextsAsync(AppStoreConnectApi api, string appId, string appInfoId, List<JsonElement> versionLocalizations, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        var locales = new SortedSet<string>(StringComparer.Ordinal);

        if (appInfoId != null)
        {
            var (infoLocalizations, _) = await api.GetAllAsync($"/v1/appInfos/{appInfoId}/appInfoLocalizations?limit=50", cancellationToken);

            foreach (var localization in infoLocalizations)
            {
                var locale = JsonRead.Attr(localization, "locale");

                locales.Add(locale);

                foreach (var field in _appInfoFields)
                    snapshot.Values[ContentKey.Of(ContentGroups.AppInfo, locale, field)] = JsonRead.Attr(localization, field);
            }
        }

        foreach (var localization in versionLocalizations)
        {
            var locale = JsonRead.Attr(localization, "locale");

            locales.Add(locale);

            foreach (var field in _versionLocalizationFields)
                snapshot.Values[ContentKey.Of(ContentGroups.Version, locale, field)] = JsonRead.Attr(localization, field);
        }

        using var eula = await api.GetOrNullAsync($"/v1/apps/{appId}/endUserLicenseAgreement", cancellationToken);

        if (eula != null)
            snapshot.Values[ContentKey.Of(ContentGroups.App, null, "eula")] = JsonRead.Attr(JsonRead.Get(eula.RootElement, "data"), "agreementText");

        snapshot.Locales = [.. locales.Where(l => l != null)];
    }

    private static async Task ReadCategoriesAsync(AppStoreConnectApi api, string appInfoId, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        using var info = await api.GetAsync($"/v1/appInfos/{appInfoId}?include=primaryCategory,secondaryCategory", cancellationToken);
        var data = JsonRead.Get(info.RootElement, "data");

        snapshot.Values[ContentKey.Of(ContentGroups.AppInfo, null, "primaryCategory")] = JsonRead.RelId(data, "primaryCategory");
        snapshot.Values[ContentKey.Of(ContentGroups.AppInfo, null, "secondaryCategory")] = JsonRead.RelId(data, "secondaryCategory");
    }

    private static async Task ReadAgeRatingAsync(AppStoreConnectApi api, string appInfoId, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        using var declaration = await api.GetOrNullAsync($"/v1/appInfos/{appInfoId}/ageRatingDeclaration", cancellationToken);

        if (declaration == null)
            return;

        var attributes = JsonRead.Get(JsonRead.Get(declaration.RootElement, "data"), "attributes");

        if (attributes.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in attributes.EnumerateObject())
            snapshot.Values[ContentKey.Of(ContentGroups.AgeRating, null, property.Name)] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetRawText();
    }

    private async Task ReadPricingAsync(AppStoreConnectApi api, string appId, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        using (var schedule = await api.GetOrNullAsync($"/v1/apps/{appId}/appPriceSchedule?include=baseTerritory", cancellationToken))
        {
            if (schedule != null)
            {
                var data = JsonRead.Get(schedule.RootElement, "data");
                var scheduleId = JsonRead.Str(data, "id");
                var baseTerritory = JsonRead.RelId(data, "baseTerritory");

                snapshot.Values[ContentKey.Of(ContentGroups.Pricing, null, "baseTerritory")] = baseTerritory;

                var (prices, included) = await api.GetAllAsync($"/v1/appPriceSchedules/{scheduleId}/manualPrices?include=appPricePoint,territory&limit=200", cancellationToken);

                snapshot.Prices = [.. prices.Select(p =>
                {
                    var pointId = JsonRead.RelId(p, "appPricePoint");
                    var point = included.FirstOrDefault(i => JsonRead.Str(i, "type") == "appPricePoints" && JsonRead.Str(i, "id") == pointId);

                    return new PriceEntryState
                    {
                        Territory = JsonRead.RelId(p, "territory") ?? baseTerritory,
                        PricePointId = pointId,
                        CustomerPrice = point.ValueKind == JsonValueKind.Object ? JsonRead.Attr(point, "customerPrice") : null,
                        StartDate = JsonRead.Day(JsonRead.Get(p, "attributes"), "startDate"),
                        EndDate = JsonRead.Day(JsonRead.Get(p, "attributes"), "endDate"),
                    };
                })];

                var today = DateOnly.FromDateTime(_http.Clock.GetUtcNow().UtcDateTime);
                var current = snapshot.Prices.FirstOrDefault(p => p.Territory == baseTerritory && (p.StartDate == null || p.StartDate <= today) && (p.EndDate == null || p.EndDate > today));

                snapshot.Values[ContentKey.Of(ContentGroups.Pricing, null, "pricePoint")] = current?.PricePointId;
            }
        }

        using var availability = await api.GetOrNullAsync($"/v1/apps/{appId}/appAvailabilityV2", cancellationToken);

        if (availability == null)
            return;

        var availabilityData = JsonRead.Get(availability.RootElement, "data");

        snapshot.AvailableInNewTerritories = JsonRead.Attr(availabilityData, "availableInNewTerritories") == "true";

        var (territories, _) = await api.GetAllAsync($"/v2/appAvailabilities/{JsonRead.Str(availabilityData, "id")}/territoryAvailabilities?include=territory&limit=200", cancellationToken);

        foreach (var territory in territories)
            snapshot.Values[ContentKey.Of(ContentGroups.Availability, null, JsonRead.RelId(territory, "territory"))] = JsonRead.Attr(territory, "available") == "true" ? "true" : "false";
    }

    private static async Task ReadMediaAsync(AppStoreConnectApi api, List<JsonElement> versionLocalizations, MediaKind kind, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        foreach (var localization in versionLocalizations)
        {
            var locale = JsonRead.Attr(localization, "locale");

            foreach (var set in await ListSetsAsync(api, JsonRead.Str(localization, "id"), kind, cancellationToken))
            {
                set.Locale = locale;
                snapshot.Media.Add(set);
            }
        }
    }

    private static async Task<List<MediaSetState>> ListSetsAsync(AppStoreConnectApi api, string localizationId, MediaKind kind, CancellationToken cancellationToken)
    {
        var (setsPath, itemsRelation, typeAttribute, itemType) = kind == MediaKind.Screenshot
            ? ("appScreenshotSets", "appScreenshots", "screenshotDisplayType", "appScreenshots")
            : ("appPreviewSets", "appPreviews", "previewType", "appPreviews");

        var (sets, included) = await api.GetAllAsync($"/v1/appStoreVersionLocalizations/{localizationId}/{setsPath}?include={itemsRelation}&limit=50", cancellationToken);
        var items = included.Where(i => JsonRead.Str(i, "type") == itemType).GroupBy(i => JsonRead.Str(i, "id")).ToDictionary(g => g.Key, g => g.First());

        return [.. sets.Select(set => new MediaSetState
        {
            Kind = kind,
            Id = JsonRead.Str(set, "id"),
            ClassKey = JsonRead.Attr(set, typeAttribute),
            Items = [.. JsonRead.RelIds(set, itemsRelation).Select(id => ToItem(id, items.GetValueOrDefault(id)))],
        })];
    }

    private static MediaItemState ToItem(string id, JsonElement resource)
    {
        var attributes = JsonRead.Get(resource, "attributes");
        var image = JsonRead.Get(attributes, "imageAsset");

        return new MediaItemState
        {
            Id = id,
            FileName = JsonRead.Str(attributes, "fileName"),
            Checksum = JsonRead.Str(attributes, "sourceFileChecksum")?.ToLowerInvariant(),
            Width = JsonRead.Int(image, "width"),
            Height = JsonRead.Int(image, "height"),
            State = JsonRead.Str(JsonRead.Get(attributes, "assetDeliveryState"), "state"),
            ImageUrl = ImageUrlOf(JsonRead.Str(image, "templateUrl")),
        };
    }

    // The asset's template address ({w}x{h}bb.{f}) filled for a 1200 pixel bounding box as JPEG; null without one.
    private static string ImageUrlOf(string templateUrl)
        => string.IsNullOrWhiteSpace(templateUrl) || !templateUrl.Contains("{w}", StringComparison.Ordinal)
            ? null
            : templateUrl.Replace("{w}", "1200", StringComparison.Ordinal).Replace("{h}", "1200", StringComparison.Ordinal).Replace("{f}", "jpg", StringComparison.Ordinal);

    private static async Task ReadReviewAsync(AppStoreConnectApi api, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        using var detail = await api.GetOrNullAsync($"/v1/appStoreVersions/{snapshot.VersionId}/appStoreReviewDetail", cancellationToken);

        if (detail == null)
            return;

        var data = JsonRead.Get(detail.RootElement, "data");

        foreach (var field in _reviewFields)
            snapshot.Values[ContentKey.Of(ContentGroups.Review, null, field)] = JsonRead.Attr(data, field);

        snapshot.StoreDemoPasswordSet = !string.IsNullOrEmpty(JsonRead.Attr(data, "demoAccountPassword"));
        snapshot.Values[ContentKey.Of(ContentGroups.Review, null, "demoAccountPassword")] = snapshot.StoreDemoPasswordSet ? MaskedSecret : null;
    }

    private static async Task ReadBuildAsync(AppStoreConnectApi api, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        var buildId = snapshot.Versions.FirstOrDefault(v => v.Id == snapshot.VersionId)?.BuildId;

        if (buildId == null)
            return;

        using var build = await api.GetOrNullAsync($"/v1/builds/{buildId}?fields[builds]=version,processingState,usesNonExemptEncryption,expired", cancellationToken);

        if (build == null)
            return;

        var data = JsonRead.Get(build.RootElement, "data");
        var uses = JsonRead.Attr(data, "usesNonExemptEncryption");

        snapshot.Build = new AttachedBuildState
        {
            Id = buildId,
            BuildNumber = JsonRead.Attr(data, "version"),
            ProcessingState = JsonRead.Attr(data, "expired") == "true" ? BuildProcessingState.Expired : MapBuildState(JsonRead.Attr(data, "processingState")),
            UsesNonExemptEncryption = uses == null ? null : uses == "true",
        };

        snapshot.Values[ContentKey.Of(ContentGroups.Build, null, "usesNonExemptEncryption")] = uses;
    }

    private static BuildProcessingState MapBuildState(string state) => state switch
    {
        "VALID" => BuildProcessingState.Valid,
        "FAILED" => BuildProcessingState.Failed,
        "INVALID" => BuildProcessingState.Invalid,
        _ => BuildProcessingState.Processing,
    };
}
