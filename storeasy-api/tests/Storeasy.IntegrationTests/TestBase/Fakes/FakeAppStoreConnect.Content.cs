using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Localizations, screenshots, builds, review details and submissions of the fake App Store Connect.
/// </summary>
public sealed partial class FakeAppStoreConnect
{
    private HttpResponseMessage RouteContent(string method, string path, System.Collections.Specialized.NameValueCollection query, JsonObject body)
    {
        Match m;

        if (method == "PATCH" && (m = Path("^/v1/appInfoLocalizations/([^/]+)$", path)).Success)
            return Patch(InfoLocalizations, "appInfoLocalizations", m.Groups[1].Value, body);

        if (method == "POST" && path == "/v1/appInfoLocalizations")
        {
            var infoId = body["data"]["relationships"]["appInfo"]["data"]["id"].GetValue<string>();
            var attributes = (JsonObject)body["data"]["attributes"].DeepClone();
            var locale = (string)attributes["locale"];
            var id = NewId("il");

            attributes["info"] = infoId;
            InfoLocalizations[id] = attributes;

            // Apple creates the version localization of the same locale on the editable version (store-ops finding).
            var appId = (string)AppInfos[infoId]["app"];
            var version = Versions.First(v => (string)v.Value["app"] == appId && (string)v.Value["appVersionState"] == "PREPARE_FOR_SUBMISSION").Key;

            if (!VersionLocalizations.Values.Any(l => (string)l["version"] == version && (string)l["locale"] == locale))
                VersionLocalizations[NewId("vl")] = new JsonObject { ["version"] = version, ["locale"] = locale };

            return Ok(new JsonObject { ["data"] = Resource("appInfoLocalizations", id, attributes) }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/appStoreVersionLocalizations/([^/]+)$", path)).Success)
            return Patch(VersionLocalizations, "appStoreVersionLocalizations", m.Groups[1].Value, body);

        if (method == "POST" && path == "/v1/appStoreVersionLocalizations")
        {
            var versionId = body["data"]["relationships"]["appStoreVersion"]["data"]["id"].GetValue<string>();
            var attributes = (JsonObject)body["data"]["attributes"].DeepClone();

            if (VersionLocalizations.Values.Any(l => (string)l["version"] == versionId && (string)l["locale"] == (string)attributes["locale"]))
                return Conflict("The localization already exists.");

            attributes["version"] = versionId;

            var id = NewId("vl");
            VersionLocalizations[id] = attributes;

            return Ok(new JsonObject { ["data"] = Resource("appStoreVersionLocalizations", id, attributes) }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/appStoreVersions/([^/]+)/relationships/build$", path)).Success)
        {
            Versions[m.Groups[1].Value]["build"] = body["data"]["id"].GetValue<string>();

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (method == "PATCH" && (m = Path("^/v1/appStoreVersions/([^/]+)$", path)).Success)
            return Patch(Versions, "appStoreVersions", m.Groups[1].Value, body);

        if (method == "GET" && (m = Path("^/v1/appStoreVersions/([^/]+)/appStoreVersionPhasedRelease$", path)).Success)
            return NotFound();

        if (method == "GET" && (m = Path("^/v1/appStoreVersions/([^/]+)/build$", path)).Success)
        {
            var buildId = (string)Versions[m.Groups[1].Value]["build"];

            return buildId == null ? Ok(new JsonObject { ["data"] = null }) : Ok(new JsonObject { ["data"] = Resource("builds", buildId, Builds[buildId]) });
        }

        if (method == "GET" && (m = Path("^/v1/builds/([^/]+)$", path)).Success)
            return Builds.TryGetValue(m.Groups[1].Value, out var build) ? Ok(new JsonObject { ["data"] = Resource("builds", m.Groups[1].Value, build) }) : NotFound();

        if (method == "PATCH" && (m = Path("^/v1/builds/([^/]+)$", path)).Success)
            return Patch(Builds, "builds", m.Groups[1].Value, body);

        if (method == "GET" && (m = Path("^/v1/appStoreVersions/([^/]+)/appStoreReviewDetail$", path)).Success)
        {
            return ReviewDetails.TryGetValue(m.Groups[1].Value, out var detail)
                ? Ok(new JsonObject { ["data"] = Resource("appStoreReviewDetails", (string)detail["id"], Without(detail, "id")) })
                : NotFound();
        }

        if (method == "PATCH" && (m = Path("^/v1/appStoreReviewDetails/([^/]+)$", path)).Success)
        {
            var detail = ReviewDetails.Values.First(d => (string)d["id"] == m.Groups[1].Value);

            foreach (var (name, value) in body["data"]["attributes"].AsObject())
                detail[name] = value?.DeepClone();

            return Ok(new JsonObject { ["data"] = Resource("appStoreReviewDetails", m.Groups[1].Value, Without(detail, "id")) });
        }

        if (method == "GET" && (m = Path("^/v1/(?:appStoreVersionLocalizations|appStoreVersionExperimentTreatmentLocalizations|appCustomProductPageLocalizations)/([^/]+)/appScreenshotSets$", path)).Success)
        {
            var sets = ScreenshotSets.Where(s => (string)s.Value["locId"] == m.Groups[1].Value).ToList();

            return Ok(new JsonObject
            {
                ["data"] = new JsonArray([.. sets.Select(s => (JsonNode)Resource("appScreenshotSets", s.Key, s.Value, new JsonObject
                {
                    ["appScreenshots"] = new JsonObject { ["data"] = new JsonArray([.. s.Value["items"].AsArray().Select(i => (JsonNode)new JsonObject { ["type"] = "appScreenshots", ["id"] = i.GetValue<string>() })]) },
                }))]),
                ["included"] = new JsonArray([.. sets.SelectMany(s => s.Value["items"].AsArray()).Select(i => (JsonNode)ScreenshotResource(i.GetValue<string>()))]),
            });
        }

        if (method == "GET" && Path("^/v1/(?:appStoreVersionLocalizations|appStoreVersionExperimentTreatmentLocalizations|appCustomProductPageLocalizations)/[^/]+/appPreviewSets$", path).Success)
            return Ok(new JsonObject { ["data"] = new JsonArray() });

        if (method == "POST" && path == "/v1/appScreenshotSets")
        {
            var id = NewId("set");

            ScreenshotSets[id] = new JsonObject
            {
                ["locId"] = ScreenshotSetParent(body["data"]["relationships"].AsObject()),
                ["screenshotDisplayType"] = body["data"]["attributes"]["screenshotDisplayType"].GetValue<string>(),
                ["items"] = new JsonArray(),
            };

            return Ok(new JsonObject { ["data"] = Resource("appScreenshotSets", id, ScreenshotSets[id]) }, HttpStatusCode.Created);
        }

        if (method == "DELETE" && (m = Path("^/v1/appScreenshotSets/([^/]+)$", path)).Success)
        {
            ScreenshotSets.Remove(m.Groups[1].Value);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (method == "POST" && path == "/v1/appScreenshots")
        {
            var id = NewId("shot");
            var setId = body["data"]["relationships"]["appScreenshotSet"]["data"]["id"].GetValue<string>();
            var size = body["data"]["attributes"]["fileSize"].GetValue<long>();

            Screenshots[id] = new JsonObject
            {
                ["set"] = setId,
                ["fileName"] = body["data"]["attributes"]["fileName"].GetValue<string>(),
                ["fileSize"] = size,
                ["state"] = "AWAITING_UPLOAD",
                ["sourceFileChecksum"] = null,
            };

            ScreenshotSets[setId]["items"].AsArray().Add(id);

            var half = size / 2;
            var resource = ScreenshotResource(id);

            resource["attributes"]["uploadOperations"] = new JsonArray(
                new JsonObject { ["method"] = "PUT", ["url"] = $"{UploadHost}/{id}/0", ["offset"] = 0, ["length"] = half, ["requestHeaders"] = new JsonArray(new JsonObject { ["name"] = "Content-Type", ["value"] = "image/png" }) },
                new JsonObject { ["method"] = "PUT", ["url"] = $"{UploadHost}/{id}/1", ["offset"] = half, ["length"] = size - half, ["requestHeaders"] = new JsonArray() });

            return Ok(new JsonObject { ["data"] = resource }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/appScreenshots/([^/]+)$", path)).Success)
        {
            var shot = Screenshots[m.Groups[1].Value];
            var attributes = body["data"]["attributes"];
            var expected = (string)attributes["sourceFileChecksum"];
            var uploaded = Uploads.Where(u => u.Key.StartsWith($"/{m.Groups[1].Value}/", StringComparison.Ordinal)).OrderBy(u => u.Key).SelectMany(u => u.Value).ToArray();

            shot["sourceFileChecksum"] = expected;
            shot["state"] = attributes["uploaded"]?.GetValue<bool>() == true && Md5(uploaded) == expected ? "COMPLETE" : "FAILED";

            return Ok(new JsonObject { ["data"] = ScreenshotResource(m.Groups[1].Value) });
        }

        if (method == "GET" && (m = Path("^/v1/appScreenshots/([^/]+)$", path)).Success)
            return Ok(new JsonObject { ["data"] = ScreenshotResource(m.Groups[1].Value) });

        if (method == "DELETE" && (m = Path("^/v1/appScreenshots/([^/]+)$", path)).Success)
        {
            if (Screenshots.Remove(m.Groups[1].Value, out var shot))
                ScreenshotSets[(string)shot["set"]]["items"].AsArray().Remove(ScreenshotSets[(string)shot["set"]]["items"].AsArray().First(i => i.GetValue<string>() == m.Groups[1].Value));

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (method == "PATCH" && (m = Path("^/v1/appScreenshotSets/([^/]+)/relationships/appScreenshots$", path)).Success)
        {
            ScreenshotSets[m.Groups[1].Value]["items"] = new JsonArray([.. body["data"].AsArray().Select(d => (JsonNode)d["id"].GetValue<string>())]);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (method == "GET" && path == "/v1/reviewSubmissions")
            return Ok(new JsonObject { ["data"] = new JsonArray() });

        if (method == "POST" && path == "/v1/reviewSubmissions")
            return Ok(new JsonObject { ["data"] = new JsonObject { ["type"] = "reviewSubmissions", ["id"] = NewId("sub"), ["attributes"] = new JsonObject { ["state"] = "READY_FOR_REVIEW" } } }, HttpStatusCode.Created);

        if (method == "POST" && path == "/v1/reviewSubmissionItems")
        {
            if (AddExperimentSubmissionItem(body["data"]["relationships"].AsObject()))
                return Ok(new JsonObject { ["data"] = new JsonObject { ["type"] = "reviewSubmissionItems", ["id"] = NewId("item") } }, HttpStatusCode.Created);

            SubmittedVersions.Add(body["data"]["relationships"]["appStoreVersion"]["data"]["id"].GetValue<string>());

            return Ok(new JsonObject { ["data"] = new JsonObject { ["type"] = "reviewSubmissionItems", ["id"] = NewId("item") } }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Path("^/v1/reviewSubmissions/([^/]+)$", path)).Success)
        {
            foreach (var versionId in SubmittedVersions)
                Versions[versionId]["appVersionState"] = "WAITING_FOR_REVIEW";

            SubmitExperimentItems();

            return Ok(new JsonObject { ["data"] = new JsonObject { ["type"] = "reviewSubmissions", ["id"] = m.Groups[1].Value, ["attributes"] = new JsonObject { ["state"] = "WAITING_FOR_REVIEW" } } });
        }

        return RouteExperiments(method, path, body)
               ?? RouteMonetization(method, path, query, body)
               ?? new HttpResponseMessage(HttpStatusCode.NotImplemented) { Content = new StringContent($"Fake App Store Connect has no route for {method} {path}") };
    }

    private HttpResponseMessage Patch(Dictionary<string, JsonObject> store, string type, string id, JsonObject body)
    {
        if (!store.TryGetValue(id, out var item))
            return NotFound();

        foreach (var (name, value) in body["data"]["attributes"]?.AsObject() ?? [])
            item[name] = value?.DeepClone();

        return Ok(new JsonObject { ["data"] = Resource(type, id, item) });
    }

    private JsonObject ScreenshotResource(string id)
    {
        var shot = Screenshots[id];

        return new JsonObject
        {
            ["type"] = "appScreenshots",
            ["id"] = id,
            ["attributes"] = new JsonObject
            {
                ["fileName"] = (string)shot["fileName"],
                ["fileSize"] = (long)shot["fileSize"],
                ["sourceFileChecksum"] = (string)shot["sourceFileChecksum"],
                ["imageAsset"] = new JsonObject { ["width"] = 2064, ["height"] = 2752 },
                ["assetDeliveryState"] = new JsonObject { ["state"] = (string)shot["state"] },
            },
        };
    }

    private static JsonObject Without(JsonObject source, string key)
    {
        var copy = (JsonObject)source.DeepClone();
        copy.Remove(key);

        return copy;
    }
}
