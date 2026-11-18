using Storeasy.Application.Interfaces;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// Writes of the App Store Connect content engine. Every write is one job step; what already matches is skipped.
/// </summary>
public sealed partial class AppleContentGateway
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var appId = Uri.EscapeDataString(target.ExternalId);
        var versionId = changes.VersionId ?? target.VersionId;
        var outcome = new List<string>();
        var fields = changes.Fields;

        // 1. App info localizations first: creating one can create the version localization of the same locale.
        foreach (var group in fields.Where(f => f.Group == ContentGroups.AppInfo && f.Locale != null).GroupBy(f => f.Locale))
            outcome.Add(await ContentSteps.RunAsync(context, "appInfoLocalization.write", group.Key, () => WriteAppInfoLocalizationAsync(api, appId, group.Key, [.. group], cancellationToken), cancellationToken));

        foreach (var group in fields.Where(f => f.Group == ContentGroups.Version && f.Locale != null).GroupBy(f => f.Locale))
            outcome.Add(await ContentSteps.RunAsync(context, "versionLocalization.write", group.Key, () => WriteVersionLocalizationAsync(api, RequireVersion(versionId), group.Key, [.. group], cancellationToken), cancellationToken));

        var appFields = fields.Where(f => f.Group == ContentGroups.App).ToList();

        if (appFields.Any(f => f.Field == "contentRightsDeclaration"))
        {
            outcome.Add(await ContentSteps.RunAsync(context, "app.update", "contentRightsDeclaration", async () =>
            {
                using var _ = await api.PatchAsync($"/v1/apps/{appId}", Resource("apps", target.ExternalId, Attributes(appFields.Where(f => f.Field == "contentRightsDeclaration"))), cancellationToken);
                return "Content rights declaration written.";
            }, cancellationToken));
        }

        if (appFields.FirstOrDefault(f => f.Field == "eula") is { } eula)
            outcome.Add(await ContentSteps.RunAsync(context, "eula.write", null, () => WriteEulaAsync(api, appId, target.ExternalId, eula.Value, cancellationToken), cancellationToken));

        var categories = fields.Where(f => f.Group == ContentGroups.AppInfo && f.Locale == null).ToList();

        if (categories.Count > 0)
            outcome.Add(await ContentSteps.RunAsync(context, "appInfo.categories", null, () => WriteCategoriesAsync(api, appId, categories, cancellationToken), cancellationToken));

        var versionFields = fields.Where(f => f.Group == ContentGroups.Version && f.Locale == null).ToList();

        if (versionFields.Count > 0)
        {
            outcome.Add(await ContentSteps.RunAsync(context, "version.update", string.Join(", ", versionFields.Select(f => f.Field)), async () =>
            {
                using var _ = await api.PatchAsync($"/v1/appStoreVersions/{RequireVersion(versionId)}", Resource("appStoreVersions", versionId, Attributes(versionFields)), cancellationToken);
                return "Version updated.";
            }, cancellationToken));
        }

        var ageRating = fields.Where(f => f.Group == ContentGroups.AgeRating).ToList();

        if (ageRating.Count > 0)
            outcome.Add(await ContentSteps.RunAsync(context, "ageRating.update", $"{ageRating.Count} answers", () => WriteAgeRatingAsync(api, appId, ageRating, cancellationToken), cancellationToken));

        var pricing = fields.Where(f => f.Group == ContentGroups.Pricing).ToList();

        if (pricing.Count > 0)
            outcome.Add(await ContentSteps.RunAsync(context, "priceSchedule.create", null, () => WritePriceAsync(api, target.ExternalId, pricing, cancellationToken), cancellationToken));

        var availability = fields.Where(f => f.Group == ContentGroups.Availability).ToList();

        if (availability.Count > 0)
            outcome.Add(await ContentSteps.RunAsync(context, "territoryAvailability.update", $"{availability.Count} territories", () => WriteAvailabilityAsync(api, appId, availability, cancellationToken), cancellationToken));

        var review = fields.Where(f => f.Group == ContentGroups.Review).ToList();

        if (review.Count > 0 || changes.DemoPasswordFromAnswers)
            outcome.Add(await ContentSteps.RunAsync(context, "reviewDetail.write", null, () => WriteReviewDetailAsync(api, RequireVersion(versionId), review, changes, cancellationToken), cancellationToken));

        var build = fields.Where(f => f.Group == ContentGroups.Build).ToList();

        if (build.FirstOrDefault(f => f.Field == "build") is { } attach)
            outcome.Add(await ContentSteps.RunAsync(context, "version.build", attach.Value, () => AttachBuildAsync(api, RequireVersion(versionId), attach.Value, cancellationToken), cancellationToken));

        if (build.FirstOrDefault(f => f.Field == "usesNonExemptEncryption") is { } compliance)
            outcome.Add(await ContentSteps.RunAsync(context, "build.exportCompliance", compliance.Value, () => WriteExportComplianceAsync(api, RequireVersion(versionId), build, cancellationToken), cancellationToken));

        if (fields.FirstOrDefault(f => f.Group == ContentGroups.Phased) is { } phased)
            outcome.Add(await ContentSteps.RunAsync(context, "phasedRelease.write", phased.Value, () => WritePhasedReleaseAsync(api, RequireVersion(versionId), phased.Value, cancellationToken), cancellationToken));

        foreach (var set in changes.Media)
            outcome.Add(await SyncMediaSetAsync(api, RequireVersion(versionId), set, context, cancellationToken));

        if (changes.Rollout != null)
        {
            var state = changes.Rollout.Action switch
            {
                RolloutAction.Pause => "PAUSED",
                RolloutAction.Resume => "ACTIVE",
                RolloutAction.Complete => "COMPLETE",
                _ => throw new StoreCallException($"{changes.Rollout.Action} is not an App Store phased release action.", null, false),
            };

            outcome.Add(await ContentSteps.RunAsync(context, "phasedRelease.write", state, () => WritePhasedReleaseAsync(api, RequireVersion(versionId), state, cancellationToken), cancellationToken));
        }

        if (changes.SubmitForReview)
            outcome.Add(await ContentSteps.RunAsync(context, "reviewSubmission.submit", versionId, () => SubmitAsync(api, target.ExternalId, RequireVersion(versionId), cancellationToken), cancellationToken));

        return outcome;
    }

    private static string RequireVersion(string versionId) => versionId ?? throw new StoreCallException("The change needs an App Store version and none was given.", null, false);

    private static async Task<string> WriteAppInfoLocalizationAsync(AppStoreConnectApi api, string appId, string locale, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        var appInfoId = await EditableAppInfoIdAsync(api, appId, cancellationToken) ?? throw new StoreCallException("The app has no app info.", null, false);
        var (localizations, _) = await api.GetAllAsync($"/v1/appInfos/{appInfoId}/appInfoLocalizations?limit=50", cancellationToken);
        var existing = localizations.FirstOrDefault(l => JsonRead.Attr(l, "locale") == locale);

        if (existing.ValueKind == JsonValueKind.Object)
        {
            var id = JsonRead.Str(existing, "id");

            using var _ = await api.PatchAsync($"/v1/appInfoLocalizations/{id}", Resource("appInfoLocalizations", id, Attributes(changes)), cancellationToken);

            return $"{locale} app info updated.";
        }

        var attributes = Attributes(changes);
        attributes["locale"] = locale;

        using var created = await api.PostAsync("/v1/appInfoLocalizations", new
        {
            data = new { type = "appInfoLocalizations", attributes, relationships = new { appInfo = Relation("appInfos", appInfoId) } },
        }, cancellationToken);

        return $"{locale} app info created.";
    }

    /// <summary>
    /// PATCHes the version localization of <paramref name="locale"/> when it exists (Apple may have created it when the app
    /// info localization was created), creates it otherwise and falls back to PATCH when the create answers 409.
    /// </summary>
    private static async Task<string> WriteVersionLocalizationAsync(AppStoreConnectApi api, string versionId, string locale, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        var existing = await FindVersionLocalizationAsync(api, versionId, locale, cancellationToken);

        if (existing != null)
        {
            using var _ = await api.PatchAsync($"/v1/appStoreVersionLocalizations/{existing}", Resource("appStoreVersionLocalizations", existing, Attributes(changes)), cancellationToken);

            return $"{locale} version texts updated.";
        }

        try
        {
            var attributes = Attributes(changes);
            attributes["locale"] = locale;

            using var created = await api.PostAsync("/v1/appStoreVersionLocalizations", new
            {
                data = new { type = "appStoreVersionLocalizations", attributes, relationships = new { appStoreVersion = Relation("appStoreVersions", versionId) } },
            }, cancellationToken);

            return $"{locale} version texts created.";
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            var again = await FindVersionLocalizationAsync(api, versionId, locale, cancellationToken);

            if (again == null)
                throw;

            using var _ = await api.PatchAsync($"/v1/appStoreVersionLocalizations/{again}", Resource("appStoreVersionLocalizations", again, Attributes(changes)), cancellationToken);

            return $"{locale} version texts updated after a conflicting create.";
        }
    }

    private static async Task<string> FindVersionLocalizationAsync(AppStoreConnectApi api, string versionId, string locale, CancellationToken cancellationToken)
    {
        var (localizations, _) = await api.GetAllAsync($"/v1/appStoreVersions/{versionId}/appStoreVersionLocalizations?limit=50", cancellationToken);

        return localizations.Where(l => JsonRead.Attr(l, "locale") == locale).Select(l => JsonRead.Str(l, "id")).FirstOrDefault();
    }

    private static async Task<string> EnsureVersionLocalizationAsync(AppStoreConnectApi api, string versionId, string locale, CancellationToken cancellationToken)
    {
        var existing = await FindVersionLocalizationAsync(api, versionId, locale, cancellationToken);

        if (existing != null)
            return existing;

        try
        {
            using var created = await api.PostAsync("/v1/appStoreVersionLocalizations", new
            {
                data = new { type = "appStoreVersionLocalizations", attributes = new { locale }, relationships = new { appStoreVersion = Relation("appStoreVersions", versionId) } },
            }, cancellationToken);

            return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            var again = await FindVersionLocalizationAsync(api, versionId, locale, cancellationToken);

            if (again == null)
                throw;

            return again;
        }
    }

    private static async Task<string> WriteEulaAsync(AppStoreConnectApi api, string appId, string externalId, string text, CancellationToken cancellationToken)
    {
        using var current = await api.GetOrNullAsync($"/v1/apps/{appId}/endUserLicenseAgreement", cancellationToken);

        if (current != null)
        {
            var id = JsonRead.Str(JsonRead.Get(current.RootElement, "data"), "id");

            if (string.IsNullOrEmpty(text))
            {
                await api.DeleteAsync($"/v1/endUserLicenseAgreements/{id}", cancellationToken);
                return "Custom EULA removed; the standard EULA applies.";
            }

            using var _ = await api.PatchAsync($"/v1/endUserLicenseAgreements/{id}", Resource("endUserLicenseAgreements", id, new JsonObject { ["agreementText"] = text }), cancellationToken);

            return "EULA updated.";
        }

        if (string.IsNullOrEmpty(text))
            return "No custom EULA to remove.";

        var (territories, _) = await api.GetAllAsync("/v1/territories?limit=200", cancellationToken);

        using var created = await api.PostAsync("/v1/endUserLicenseAgreements", new
        {
            data = new
            {
                type = "endUserLicenseAgreements",
                attributes = new { agreementText = text },
                relationships = new
                {
                    app = Relation("apps", externalId),
                    territories = new { data = territories.Select(t => new { type = "territories", id = JsonRead.Str(t, "id") }).ToList() },
                },
            },
        }, cancellationToken);

        return "EULA created.";
    }

    private static async Task<string> WriteCategoriesAsync(AppStoreConnectApi api, string appId, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        var appInfoId = await EditableAppInfoIdAsync(api, appId, cancellationToken) ?? throw new StoreCallException("The app has no app info.", null, false);
        var relationships = new JsonObject();

        foreach (var change in changes)
            relationships[change.Field] = new JsonObject { ["data"] = string.IsNullOrEmpty(change.Value) ? null : new JsonObject { ["type"] = "appCategories", ["id"] = change.Value } };

        using var _ = await api.PatchAsync($"/v1/appInfos/{appInfoId}", new JsonObject
        {
            ["data"] = new JsonObject { ["type"] = "appInfos", ["id"] = appInfoId, ["relationships"] = relationships },
        }, cancellationToken);

        return "Categories written.";
    }

    private static async Task<string> WriteAgeRatingAsync(AppStoreConnectApi api, string appId, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        var appInfoId = await EditableAppInfoIdAsync(api, appId, cancellationToken) ?? throw new StoreCallException("The app has no app info.", null, false);

        using var declaration = await api.GetAsync($"/v1/appInfos/{appInfoId}/ageRatingDeclaration", cancellationToken);
        var id = JsonRead.Str(JsonRead.Get(declaration.RootElement, "data"), "id");

        // Age rating values are raw JSON (enum strings, booleans); they are written as they are.
        var attributes = new JsonObject();

        foreach (var change in changes)
            attributes[change.Field] = string.IsNullOrEmpty(change.Value) ? null : JsonNode.Parse(change.Value);

        using var _ = await api.PatchAsync($"/v1/ageRatingDeclarations/{id}", Resource("ageRatingDeclarations", id, attributes), cancellationToken);

        return "Age rating declaration updated.";
    }

    private static async Task<string> WritePriceAsync(AppStoreConnectApi api, string externalId, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        var pricePoint = changes.FirstOrDefault(c => c.Field == "pricePoint")?.Value ?? throw new StoreCallException("A price change needs a price point.", null, false);
        var baseTerritory = changes.FirstOrDefault(c => c.Field == "baseTerritory")?.Value ?? "USA";

        using var _ = await api.PostAsync("/v1/appPriceSchedules", new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "appPriceSchedules",
                ["relationships"] = new JsonObject
                {
                    ["app"] = RelationNode("apps", externalId),
                    ["baseTerritory"] = RelationNode("territories", baseTerritory),
                    ["manualPrices"] = new JsonObject { ["data"] = new JsonArray(new JsonObject { ["type"] = "appPrices", ["id"] = "${price1}" }) },
                },
            },
            ["included"] = new JsonArray(new JsonObject
            {
                ["type"] = "appPrices",
                ["id"] = "${price1}",
                ["attributes"] = new JsonObject { ["startDate"] = null },
                ["relationships"] = new JsonObject { ["appPricePoint"] = RelationNode("appPricePoints", pricePoint) },
            }),
        }, cancellationToken);

        return $"Price schedule set ({baseTerritory}).";
    }

    private static async Task<string> WriteAvailabilityAsync(AppStoreConnectApi api, string appId, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        using var availability = await api.GetAsync($"/v1/apps/{appId}/appAvailabilityV2", cancellationToken);
        var availabilityId = JsonRead.Str(JsonRead.Get(availability.RootElement, "data"), "id");
        var (territories, _) = await api.GetAllAsync($"/v2/appAvailabilities/{availabilityId}/territoryAvailabilities?include=territory&limit=200", cancellationToken);
        var byTerritory = territories.GroupBy(t => JsonRead.RelId(t, "territory")).ToDictionary(g => g.Key ?? string.Empty, g => g.First());
        var written = 0;

        foreach (var change in changes)
        {
            if (!byTerritory.TryGetValue(change.Field, out var territory))
                throw new StoreCallException($"Territory {change.Field} is not in the app's availability.", null, false);

            var id = JsonRead.Str(territory, "id");

            using var _ = await api.PatchAsync($"/v1/territoryAvailabilities/{id}", Resource("territoryAvailabilities", id, new JsonObject { ["available"] = change.Value == "true" }), cancellationToken);

            written++;
        }

        return $"{written} territories updated.";
    }

    private static async Task<string> WriteReviewDetailAsync(AppStoreConnectApi api, string versionId, List<FieldChange> changes, ContentChangeSet set, CancellationToken cancellationToken)
    {
        var attributes = new JsonObject();

        foreach (var change in changes.Where(c => c.Field != "demoAccountPassword"))
            attributes[change.Field] = change.Field == "demoAccountRequired" ? change.Value == "true" : change.Value;

        if (set.DemoPasswordFromAnswers && set.Secrets.TryGetValue(DemoPasswordSecret, out var password))
            attributes["demoAccountPassword"] = password;

        if (attributes.Count == 0)
            return "Nothing to write.";

        using var current = await api.GetOrNullAsync($"/v1/appStoreVersions/{versionId}/appStoreReviewDetail", cancellationToken);

        if (current != null)
        {
            var id = JsonRead.Str(JsonRead.Get(current.RootElement, "data"), "id");

            using var _ = await api.PatchAsync($"/v1/appStoreReviewDetails/{id}", Resource("appStoreReviewDetails", id, attributes), cancellationToken);

            return "Review details updated.";
        }

        using var created = await api.PostAsync("/v1/appStoreReviewDetails", new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "appStoreReviewDetails",
                ["attributes"] = attributes,
                ["relationships"] = new JsonObject { ["appStoreVersion"] = RelationNode("appStoreVersions", versionId) },
            },
        }, cancellationToken);

        return "Review details created.";
    }

    private static async Task<string> AttachBuildAsync(AppStoreConnectApi api, string versionId, string buildId, CancellationToken cancellationToken)
    {
        using (var build = await api.GetAsync($"/v1/builds/{Uri.EscapeDataString(buildId)}?fields[builds]=processingState,expired", cancellationToken))
        {
            var data = JsonRead.Get(build.RootElement, "data");
            var state = JsonRead.Attr(data, "processingState");

            if (state != "VALID" || JsonRead.Attr(data, "expired") == "true")
                throw new StoreCallException($"Build {buildId} is {state}; only VALID builds can be attached.", null, false);
        }

        using var _ = await api.PatchAsync($"/v1/appStoreVersions/{versionId}/relationships/build", new { data = new { type = "builds", id = buildId } }, cancellationToken);

        return $"Build {buildId} attached.";
    }

    private static async Task<string> WriteExportComplianceAsync(AppStoreConnectApi api, string versionId, List<FieldChange> changes, CancellationToken cancellationToken)
    {
        var buildId = changes.FirstOrDefault(c => c.Field is "build" or "buildId")?.Value;

        if (buildId == null)
        {
            using var attached = await api.GetOrNullAsync($"/v1/appStoreVersions/{versionId}/build", cancellationToken);

            buildId = attached == null ? null : JsonRead.Str(JsonRead.Get(attached.RootElement, "data"), "id");
        }

        if (buildId == null)
            throw new StoreCallException("No build is attached to answer export compliance for.", null, false);

        var value = changes.First(c => c.Field == "usesNonExemptEncryption").Value == "true";

        using var _ = await api.PatchAsync($"/v1/builds/{buildId}", Resource("builds", buildId, new JsonObject { ["usesNonExemptEncryption"] = value }), cancellationToken);

        return $"Export compliance of build {buildId} answered.";
    }

    private static async Task<string> WritePhasedReleaseAsync(AppStoreConnectApi api, string versionId, string state, CancellationToken cancellationToken)
    {
        using var current = await api.GetOrNullAsync($"/v1/appStoreVersions/{versionId}/appStoreVersionPhasedRelease", cancellationToken);
        var id = current == null ? null : JsonRead.Str(JsonRead.Get(current.RootElement, "data"), "id");

        if (string.IsNullOrEmpty(state))
        {
            if (id != null)
                await api.DeleteAsync($"/v1/appStoreVersionPhasedReleases/{id}", cancellationToken);

            return "Phased release turned off.";
        }

        if (id != null)
        {
            using var _ = await api.PatchAsync($"/v1/appStoreVersionPhasedReleases/{id}", Resource("appStoreVersionPhasedReleases", id, new JsonObject { ["phasedReleaseState"] = state }), cancellationToken);

            return $"Phased release {state}.";
        }

        using var created = await api.PostAsync("/v1/appStoreVersionPhasedReleases", new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "appStoreVersionPhasedReleases",
                ["attributes"] = new JsonObject { ["phasedReleaseState"] = state },
                ["relationships"] = new JsonObject { ["appStoreVersion"] = RelationNode("appStoreVersions", versionId) },
            },
        }, cancellationToken);

        return $"Phased release created ({state}).";
    }

    /// <summary>
    /// Submits the version: reuses an open review submission of the app or creates one, adds the version and marks the
    /// submission submitted.
    /// </summary>
    private static async Task<string> SubmitAsync(AppStoreConnectApi api, string externalId, string versionId, CancellationToken cancellationToken)
    {
        var (open, _) = await api.GetAllAsync($"/v1/reviewSubmissions?filter[app]={Uri.EscapeDataString(externalId)}&filter[state]=READY_FOR_REVIEW&filter[platform]=IOS&limit=5", cancellationToken);
        var submissionId = open.Select(s => JsonRead.Str(s, "id")).FirstOrDefault();

        if (submissionId == null)
        {
            using var created = await api.PostAsync("/v1/reviewSubmissions", new
            {
                data = new { type = "reviewSubmissions", attributes = new { platform = "IOS" }, relationships = new { app = Relation("apps", externalId) } },
            }, cancellationToken);

            submissionId = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }

        try
        {
            using var item = await api.PostAsync("/v1/reviewSubmissionItems", new
            {
                data = new
                {
                    type = "reviewSubmissionItems",
                    relationships = new { reviewSubmission = Relation("reviewSubmissions", submissionId), appStoreVersion = Relation("appStoreVersions", versionId) },
                },
            }, cancellationToken);
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            // The version is already an item of the open submission (a retried job).
        }

        using var submitted = await api.PatchAsync($"/v1/reviewSubmissions/{submissionId}", Resource("reviewSubmissions", submissionId, new JsonObject { ["submitted"] = true }), cancellationToken);

        return $"Submitted for review ({JsonRead.Attr(JsonRead.Get(submitted.RootElement, "data"), "state") ?? "submitted"}).";
    }

    private static JsonObject Attributes(IEnumerable<FieldChange> changes)
    {
        var attributes = new JsonObject();

        foreach (var change in changes)
            attributes[change.Field] = change.Value;

        return attributes;
    }

    private static JsonObject Resource(string type, string id, JsonObject attributes)
        => new() { ["data"] = new JsonObject { ["type"] = type, ["id"] = id, ["attributes"] = attributes } };

    private static object Relation(string type, string id) => new { data = new { type, id } };

    private static JsonObject RelationNode(string type, string id) => new() { ["data"] = new JsonObject { ["type"] = type, ["id"] = id } };
}
