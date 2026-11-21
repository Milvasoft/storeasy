using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores.Content;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Experiments;

/// <summary>
/// JSON:API bodies and parsing of App Store Connect experiments (v2), treatments, treatment localizations and custom
/// product pages. Pure, so the shapes are tested against recorded answers. Resource types and relationship names follow
/// the App Store Connect API reference: a v2 experiment is created with the <c>app</c> relationship and its
/// <c>name</c>, <c>platform</c> and <c>trafficProportion</c>, started and stopped through its <c>started</c> attribute; a
/// treatment hangs under <c>appStoreVersionExperimentV2</c>; a custom product page is created with its first version and
/// localization inline (<c>included</c>).
/// </summary>
public static class AppleExperimentPayloads
{
    /// <summary>
    /// Resource type of v2 experiments (the same type name as v1, served under /v2).
    /// </summary>
    public const string ExperimentType = "appStoreVersionExperiments";

    /// <summary>
    /// Resource type of treatments.
    /// </summary>
    public const string TreatmentType = "appStoreVersionExperimentTreatments";

    /// <summary>
    /// Resource type of treatment localizations.
    /// </summary>
    public const string TreatmentLocalizationType = "appStoreVersionExperimentTreatmentLocalizations";

    /// <summary>
    /// Resource type of custom product pages.
    /// </summary>
    public const string PageType = "appCustomProductPages";

    /// <summary>
    /// Resource type of custom product page versions.
    /// </summary>
    public const string PageVersionType = "appCustomProductPageVersions";

    /// <summary>
    /// Resource type of custom product page localizations.
    /// </summary>
    public const string PageLocalizationType = "appCustomProductPageLocalizations";

    /// <summary>
    /// Custom product page version states that can still be edited.
    /// </summary>
    public static readonly IReadOnlySet<string> EditablePageStates = new HashSet<string>(StringComparer.Ordinal) { "PREPARE_FOR_SUBMISSION", "READY_FOR_REVIEW", "REJECTED" };

    /// <summary>
    /// Body creating a v2 experiment.
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="name"></param>
    /// <param name="platform"></param>
    /// <param name="trafficProportion"></param>
    /// <returns></returns>
    public static JsonObject CreateExperiment(string appId, string name, string platform, int trafficProportion) => new()
    {
        ["data"] = new JsonObject
        {
            ["type"] = ExperimentType,
            ["attributes"] = new JsonObject { ["name"] = name, ["platform"] = platform ?? "IOS", ["trafficProportion"] = trafficProportion },
            ["relationships"] = new JsonObject { ["app"] = Rel("apps", appId) },
        },
    };

    /// <summary>
    /// Body updating a v2 experiment (name, traffic proportion, started); null values are left out.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="trafficProportion"></param>
    /// <param name="started"></param>
    /// <returns></returns>
    public static JsonObject UpdateExperiment(string id, string name = null, int? trafficProportion = null, bool? started = null)
    {
        var attributes = new JsonObject();

        if (name != null)
            attributes["name"] = name;

        if (trafficProportion != null)
            attributes["trafficProportion"] = trafficProportion.Value;

        if (started != null)
            attributes["started"] = started.Value;

        return new JsonObject { ["data"] = new JsonObject { ["type"] = ExperimentType, ["id"] = id, ["attributes"] = attributes } };
    }

    /// <summary>
    /// Body creating a treatment of a v2 experiment.
    /// </summary>
    /// <param name="experimentId"></param>
    /// <param name="name"></param>
    /// <param name="appIconName"></param>
    /// <returns></returns>
    public static JsonObject CreateTreatment(string experimentId, string name, string appIconName)
    {
        var attributes = new JsonObject { ["name"] = name };

        if (appIconName != null)
            attributes["appIconName"] = appIconName;

        return new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = TreatmentType,
                ["attributes"] = attributes,
                ["relationships"] = new JsonObject { ["appStoreVersionExperimentV2"] = Rel(ExperimentType, experimentId) },
            },
        };
    }

    /// <summary>
    /// Body updating a treatment's name and icon.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="appIconName"></param>
    /// <returns></returns>
    public static JsonObject UpdateTreatment(string id, string name, string appIconName)
        => new() { ["data"] = new JsonObject { ["type"] = TreatmentType, ["id"] = id, ["attributes"] = new JsonObject { ["name"] = name, ["appIconName"] = appIconName } } };

    /// <summary>
    /// Body creating a treatment localization.
    /// </summary>
    /// <param name="treatmentId"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public static JsonObject CreateTreatmentLocalization(string treatmentId, string locale) => new()
    {
        ["data"] = new JsonObject
        {
            ["type"] = TreatmentLocalizationType,
            ["attributes"] = new JsonObject { ["locale"] = locale },
            ["relationships"] = new JsonObject { ["appStoreVersionExperimentTreatment"] = Rel(TreatmentType, treatmentId) },
        },
    };

    /// <summary>
    /// Body creating a custom product page with its first version and one localization inline.
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="name"></param>
    /// <param name="locale"></param>
    /// <param name="promotionalText"></param>
    /// <returns></returns>
    public static JsonObject CreatePage(string appId, string name, string locale, string promotionalText)
    {
        const string versionRef = "${new-version-1}";
        const string localizationRef = "${new-localization-1}";

        var localizationAttributes = new JsonObject { ["locale"] = locale };

        if (promotionalText != null)
            localizationAttributes["promotionalText"] = promotionalText;

        return new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = PageType,
                ["attributes"] = new JsonObject { ["name"] = name },
                ["relationships"] = new JsonObject
                {
                    ["app"] = Rel("apps", appId),
                    ["appCustomProductPageVersions"] = new JsonObject { ["data"] = new JsonArray(new JsonObject { ["type"] = PageVersionType, ["id"] = versionRef }) },
                },
            },
            ["included"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = PageVersionType,
                    ["id"] = versionRef,
                    ["relationships"] = new JsonObject
                    {
                        ["appCustomProductPageLocalizations"] = new JsonObject { ["data"] = new JsonArray(new JsonObject { ["type"] = PageLocalizationType, ["id"] = localizationRef }) },
                    },
                },
                new JsonObject { ["type"] = PageLocalizationType, ["id"] = localizationRef, ["attributes"] = localizationAttributes }),
        };
    }

    /// <summary>
    /// Body renaming a custom product page.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static JsonObject UpdatePage(string id, string name)
        => new() { ["data"] = new JsonObject { ["type"] = PageType, ["id"] = id, ["attributes"] = new JsonObject { ["name"] = name } } };

    /// <summary>
    /// Body creating a new version of a custom product page (to edit a page whose version is in review or live).
    /// </summary>
    /// <param name="pageId"></param>
    /// <returns></returns>
    public static JsonObject CreatePageVersion(string pageId) => new()
    {
        ["data"] = new JsonObject
        {
            ["type"] = PageVersionType,
            ["relationships"] = new JsonObject { ["appCustomProductPage"] = Rel(PageType, pageId) },
        },
    };

    /// <summary>
    /// Body creating a custom product page localization.
    /// </summary>
    /// <param name="versionId"></param>
    /// <param name="locale"></param>
    /// <param name="promotionalText"></param>
    /// <returns></returns>
    public static JsonObject CreatePageLocalization(string versionId, string locale, string promotionalText)
    {
        var attributes = new JsonObject { ["locale"] = locale };

        if (promotionalText != null)
            attributes["promotionalText"] = promotionalText;

        return new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = PageLocalizationType,
                ["attributes"] = attributes,
                ["relationships"] = new JsonObject { ["appCustomProductPageVersion"] = Rel(PageVersionType, versionId) },
            },
        };
    }

    /// <summary>
    /// Body updating a custom product page localization's promotional text.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="promotionalText"></param>
    /// <returns></returns>
    public static JsonObject UpdatePageLocalization(string id, string promotionalText)
        => new() { ["data"] = new JsonObject { ["type"] = PageLocalizationType, ["id"] = id, ["attributes"] = new JsonObject { ["promotionalText"] = promotionalText } } };

    /// <summary>
    /// Body adding an experiment or custom product page version to a review submission.
    /// </summary>
    /// <param name="submissionId"></param>
    /// <param name="kind"></param>
    /// <param name="itemId">Experiment id, or custom product page version id.</param>
    /// <returns></returns>
    public static JsonObject SubmissionItem(string submissionId, AsoExperimentKind kind, string itemId)
    {
        var relationships = new JsonObject { ["reviewSubmission"] = Rel("reviewSubmissions", submissionId) };

        if (kind == AsoExperimentKind.CustomProductPage)
            relationships["appCustomProductPageVersion"] = Rel(PageVersionType, itemId);
        else
            relationships["appStoreVersionExperimentV2"] = Rel(ExperimentType, itemId);

        return new JsonObject { ["data"] = new JsonObject { ["type"] = "reviewSubmissionItems", ["relationships"] = relationships } };
    }

    /// <summary>
    /// An experiment resource as read (treatments are added by the caller).
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    public static StoreExperimentState ParseExperiment(JsonElement resource)
    {
        var attributes = JsonRead.Get(resource, "attributes");
        var review = JsonRead.Str(attributes, "reviewRequired");

        return new StoreExperimentState
        {
            Id = JsonRead.Str(resource, "id"),
            Name = JsonRead.Str(attributes, "name"),
            State = JsonRead.Str(attributes, "state"),
            Platform = JsonRead.Str(attributes, "platform"),
            TrafficProportion = JsonRead.Int(attributes, "trafficProportion"),
            ReviewRequired = review == null ? null : review == "true",
            StartDate = JsonRead.Date(attributes, "startDate"),
            EndDate = JsonRead.Date(attributes, "endDate"),
        };
    }

    /// <summary>
    /// A treatment resource as read.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    public static StoreTreatmentState ParseTreatment(JsonElement resource) => new()
    {
        Id = JsonRead.Str(resource, "id"),
        Name = JsonRead.Attr(resource, "name"),
        AppIconName = JsonRead.Attr(resource, "appIconName"),
        PromotedDate = JsonRead.Date(JsonRead.Get(resource, "attributes"), "promotedDate"),
    };

    /// <summary>
    /// A custom product page resource as read (version and localizations are added by the caller).
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    public static StoreCustomPageState ParsePage(JsonElement resource)
    {
        var visible = JsonRead.Attr(resource, "visible");

        return new StoreCustomPageState
        {
            Id = JsonRead.Str(resource, "id"),
            Name = JsonRead.Attr(resource, "name"),
            Url = JsonRead.Attr(resource, "url"),
            Visible = visible == null ? null : visible == "true",
        };
    }

    /// <summary>
    /// The version of a custom product page storeasy works with: the one still being edited or reviewed, otherwise the
    /// highest version (Apple numbers them "1", "2", ...).
    /// </summary>
    /// <param name="versions"></param>
    /// <returns></returns>
    public static JsonElement LatestVersion(IReadOnlyList<JsonElement> versions)
    {
        static int Number(JsonElement v) => int.TryParse(JsonRead.Attr(v, "version"), out var n) ? n : 0;

        return versions.OrderByDescending(v => JsonRead.Attr(v, "state") is "REPLACED_WITH_NEW_VERSION" ? 0 : 1)
                       .ThenByDescending(Number)
                       .FirstOrDefault();
    }

    /// <summary>
    /// A custom product page localization resource as read.
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    public static StoreCustomPageLocalization ParsePageLocalization(JsonElement resource) => new()
    {
        Id = JsonRead.Str(resource, "id"),
        Locale = JsonRead.Attr(resource, "locale"),
        PromotionalText = JsonRead.Attr(resource, "promotionalText"),
    };

    private static JsonObject Rel(string type, string id) => new() { ["data"] = new JsonObject { ["type"] = type, ["id"] = id } };
}
