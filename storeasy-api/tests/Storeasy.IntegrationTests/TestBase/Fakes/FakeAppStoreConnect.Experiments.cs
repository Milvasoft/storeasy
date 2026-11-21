using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Product page optimization (experiments v2, treatments, treatment localizations) and custom product pages (pages,
/// versions, localizations) of the fake App Store Connect. A custom product page is created with its first version and
/// localization inline, as the API documents; review submission items of experiments and page versions are recorded and
/// move to WAITING_FOR_REVIEW when the submission is submitted. Every experiment write is recorded in
/// <see cref="ExperimentWrites"/>.
/// </summary>
public sealed partial class FakeAppStoreConnect
{
    public Dictionary<string, JsonObject> Experiments { get; } = [];

    public Dictionary<string, JsonObject> Treatments { get; } = [];

    public Dictionary<string, JsonObject> TreatmentLocalizations { get; } = [];

    public Dictionary<string, JsonObject> CustomPages { get; } = [];

    public Dictionary<string, JsonObject> CustomPageVersions { get; } = [];

    public Dictionary<string, JsonObject> CustomPageLocalizations { get; } = [];

    public List<(string Type, string Id)> SubmittedItems { get; } = [];

    public List<(string Method, string Path, JsonObject Body)> ExperimentWrites { get; } = [];

    /// <summary>
    /// Adds an experiment made in App Store Connect. Returns its id.
    /// </summary>
    public string AddExperiment(string appId, string name, string state, int traffic = 30, DateTime? startDate = null)
    {
        lock (_lock)
        {
            var id = NewId("exp");

            Experiments[id] = new JsonObject
            {
                ["app"] = appId,
                ["name"] = name,
                ["platform"] = "IOS",
                ["trafficProportion"] = traffic,
                ["state"] = state,
                ["reviewRequired"] = true,
                ["startDate"] = startDate?.ToString("O"),
                ["endDate"] = null,
            };

            return id;
        }
    }

    /// <summary>
    /// Routes the experiment and custom product page paths; null when the path is not one of them.
    /// </summary>
    private HttpResponseMessage RouteExperiments(string method, string path, JsonObject body)
    {
        Match m;

        if (method != "GET" && (path.Contains("Experiment", StringComparison.Ordinal) || path.Contains("CustomProductPage", StringComparison.Ordinal)))
            ExperimentWrites.Add((method, path, (JsonObject)body?.DeepClone()));

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)/appStoreVersionExperimentsV2$", path)).Success)
            return List("appStoreVersionExperiments", Experiments.Where(e => (string)e.Value["app"] == m.Groups[1].Value));

        if (method == "POST" && path == "/v2/appStoreVersionExperiments")
        {
            var data = body["data"];
            var id = NewId("exp");
            var attributes = (JsonObject)data["attributes"].DeepClone();

            attributes["app"] = data["relationships"]["app"]["data"]["id"].GetValue<string>();
            attributes["state"] = "PREPARE_FOR_SUBMISSION";
            attributes["reviewRequired"] = true;
            Experiments[id] = attributes;

            return Ok(new JsonObject { ["data"] = Resource("appStoreVersionExperiments", id, attributes) }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v2/appStoreVersionExperiments/([^/]+)$", path)).Success)
        {
            if (!Experiments.TryGetValue(m.Groups[1].Value, out var experiment))
                return NotFound();

            foreach (var (name, value) in body["data"]["attributes"].AsObject())
            {
                if (name == "started")
                {
                    var started = value.GetValue<bool>();

                    experiment[started ? "startDate" : "endDate"] = "2026-11-21T10:00:00Z";

                    if (!started)
                        experiment["state"] = "STOPPED";
                }
                else
                {
                    experiment[name] = value?.DeepClone();
                }
            }

            return Ok(new JsonObject { ["data"] = Resource("appStoreVersionExperiments", m.Groups[1].Value, experiment) });
        }

        if (method == "GET" && (m = Path("^/v2/appStoreVersionExperiments/([^/]+)/appStoreVersionExperimentTreatments$", path)).Success)
            return List("appStoreVersionExperimentTreatments", Treatments.Where(t => (string)t.Value["experiment"] == m.Groups[1].Value).Select(t => KeyValuePair.Create(t.Key, Without(t.Value, "experiment"))));

        if (method == "POST" && path == "/v1/appStoreVersionExperimentTreatments")
        {
            var id = NewId("trt");
            var attributes = (JsonObject)body["data"]["attributes"].DeepClone();

            attributes["experiment"] = body["data"]["relationships"]["appStoreVersionExperimentV2"]["data"]["id"].GetValue<string>();
            Treatments[id] = attributes;

            return Ok(new JsonObject { ["data"] = Resource("appStoreVersionExperimentTreatments", id, Without(attributes, "experiment")) }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/appStoreVersionExperimentTreatments/([^/]+)$", path)).Success)
            return Patch(Treatments, "appStoreVersionExperimentTreatments", m.Groups[1].Value, body);

        if (method == "DELETE" && (m = Path("^/v1/appStoreVersionExperimentTreatments/([^/]+)$", path)).Success)
        {
            Treatments.Remove(m.Groups[1].Value);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (method == "GET" && (m = Path("^/v1/appStoreVersionExperimentTreatments/([^/]+)/appStoreVersionExperimentTreatmentLocalizations$", path)).Success)
            return List("appStoreVersionExperimentTreatmentLocalizations", TreatmentLocalizations.Where(l => (string)l.Value["treatment"] == m.Groups[1].Value).Select(l => KeyValuePair.Create(l.Key, Without(l.Value, "treatment"))));

        if (method == "POST" && path == "/v1/appStoreVersionExperimentTreatmentLocalizations")
        {
            var id = NewId("tloc");
            var attributes = (JsonObject)body["data"]["attributes"].DeepClone();

            attributes["treatment"] = body["data"]["relationships"]["appStoreVersionExperimentTreatment"]["data"]["id"].GetValue<string>();
            TreatmentLocalizations[id] = attributes;

            return Ok(new JsonObject { ["data"] = Resource("appStoreVersionExperimentTreatmentLocalizations", id, Without(attributes, "treatment")) }, HttpStatusCode.Created);
        }

        if (method == "GET" && (m = Path("^/v1/apps/([^/]+)/appCustomProductPages$", path)).Success)
            return List("appCustomProductPages", CustomPages.Where(p => (string)p.Value["app"] == m.Groups[1].Value));

        if (method == "GET" && (m = Path("^/v1/appCustomProductPages/([^/]+)$", path)).Success)
            return CustomPages.TryGetValue(m.Groups[1].Value, out var found) ? Ok(new JsonObject { ["data"] = Resource("appCustomProductPages", m.Groups[1].Value, found) }) : NotFound();

        if (method == "POST" && path == "/v1/appCustomProductPages")
        {
            var data = body["data"];
            var appId = data["relationships"]["app"]["data"]["id"].GetValue<string>();
            var id = NewId("cpp");
            var included = body["included"].AsArray();
            var localization = included.Single(i => (string)i["type"] == "appCustomProductPageLocalizations");
            var versionId = NewId("cppv");

            CustomPages[id] = new JsonObject { ["app"] = appId, ["name"] = (string)data["attributes"]["name"], ["url"] = $"https://apps.apple.com/app/id{appId}?ppid={id}", ["visible"] = true };
            CustomPageVersions[versionId] = new JsonObject { ["page"] = id, ["number"] = 1, ["state"] = "PREPARE_FOR_SUBMISSION" };

            var locAttributes = (JsonObject)localization["attributes"].DeepClone();
            locAttributes["version"] = versionId;
            CustomPageLocalizations[NewId("cloc")] = locAttributes;

            return Ok(new JsonObject { ["data"] = Resource("appCustomProductPages", id, CustomPages[id]) }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/appCustomProductPages/([^/]+)$", path)).Success)
            return Patch(CustomPages, "appCustomProductPages", m.Groups[1].Value, body);

        if (method == "GET" && (m = Path("^/v1/appCustomProductPages/([^/]+)/appCustomProductPageVersions$", path)).Success)
            return Ok(new JsonObject { ["data"] = new JsonArray([.. CustomPageVersions.Where(v => (string)v.Value["page"] == m.Groups[1].Value).Select(v => (JsonNode)PageVersionResource(v.Key))]) });

        if (method == "POST" && path == "/v1/appCustomProductPageVersions")
        {
            var pageId = body["data"]["relationships"]["appCustomProductPage"]["data"]["id"].GetValue<string>();
            var previous = CustomPageVersions.Where(v => (string)v.Value["page"] == pageId).OrderByDescending(v => (int)v.Value["number"]).First();
            var id = NewId("cppv");

            CustomPageVersions[id] = new JsonObject { ["page"] = pageId, ["number"] = (int)previous.Value["number"] + 1, ["state"] = "PREPARE_FOR_SUBMISSION" };

            // Apple starts a new version with the content of the previous one.
            foreach (var copy in CustomPageLocalizations.Where(l => (string)l.Value["version"] == previous.Key).ToList())
            {
                var attributes = (JsonObject)copy.Value.DeepClone();
                attributes["version"] = id;
                CustomPageLocalizations[NewId("cloc")] = attributes;
            }

            return Ok(new JsonObject { ["data"] = PageVersionResource(id) }, HttpStatusCode.Created);
        }

        if (method == "GET" && (m = Path("^/v1/appCustomProductPageVersions/([^/]+)/appCustomProductPageLocalizations$", path)).Success)
            return List("appCustomProductPageLocalizations", CustomPageLocalizations.Where(l => (string)l.Value["version"] == m.Groups[1].Value).Select(l => KeyValuePair.Create(l.Key, Without(l.Value, "version"))));

        if (method == "POST" && path == "/v1/appCustomProductPageLocalizations")
        {
            var id = NewId("cloc");
            var attributes = (JsonObject)body["data"]["attributes"].DeepClone();

            attributes["version"] = body["data"]["relationships"]["appCustomProductPageVersion"]["data"]["id"].GetValue<string>();
            CustomPageLocalizations[id] = attributes;

            return Ok(new JsonObject { ["data"] = Resource("appCustomProductPageLocalizations", id, Without(attributes, "version")) }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/appCustomProductPageLocalizations/([^/]+)$", path)).Success)
            return Patch(CustomPageLocalizations, "appCustomProductPageLocalizations", m.Groups[1].Value, body);

        return null;
    }

    /// <summary>
    /// Records a review submission item of an experiment or custom product page version; false for other items.
    /// </summary>
    private bool AddExperimentSubmissionItem(JsonObject relationships)
    {
        if (relationships["appStoreVersionExperimentV2"]?["data"]?["id"] is { } experiment)
        {
            SubmittedItems.Add(("appStoreVersionExperiments", experiment.GetValue<string>()));
            Experiments[experiment.GetValue<string>()]["state"] = "READY_FOR_REVIEW";
            return true;
        }

        if (relationships["appCustomProductPageVersion"]?["data"]?["id"] is { } version)
        {
            SubmittedItems.Add(("appCustomProductPageVersions", version.GetValue<string>()));
            CustomPageVersions[version.GetValue<string>()]["state"] = "READY_FOR_REVIEW";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Moves the submitted experiment and page version items to WAITING_FOR_REVIEW.
    /// </summary>
    private void SubmitExperimentItems()
    {
        foreach (var (type, id) in SubmittedItems)
        {
            var store = type == "appStoreVersionExperiments" ? Experiments : CustomPageVersions;

            if (store.TryGetValue(id, out var item) && (string)item["state"] == "READY_FOR_REVIEW")
                item["state"] = "WAITING_FOR_REVIEW";
        }
    }

    private JsonObject PageVersionResource(string id)
    {
        var version = CustomPageVersions[id];
        var resource = Resource("appCustomProductPageVersions", id, new JsonObject { ["state"] = (string)version["state"] });

        resource["attributes"]["version"] = ((int)version["number"]).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return resource;
    }

    private static string ScreenshotSetParent(JsonObject relationships)
        => new[] { "appStoreVersionLocalization", "appStoreVersionExperimentTreatmentLocalization", "appCustomProductPageLocalization" }
               .Select(name => relationships[name]?["data"]?["id"]?.GetValue<string>())
               .First(id => id != null);
}
