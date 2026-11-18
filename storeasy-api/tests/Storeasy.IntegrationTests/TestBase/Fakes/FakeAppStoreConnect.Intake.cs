using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The build upload resources of App Store Connect for the intake tests: <c>buildUploads</c>, <c>buildUploadFiles</c> with
/// two upload operations on the upload host, the commit with the MD5 checksum, and the build Apple "processes" from it
/// (VALID at once). Also the build list filtered by app, version and build number.
/// </summary>
public sealed partial class FakeAppStoreConnect
{
    /// <summary>
    /// Bytes received on the upload host, by path.
    /// </summary>
    public ConcurrentDictionary<string, byte[]> BuildUploadParts { get; } = new();

    /// <summary>
    /// Build uploads: id to (app, version, build number, state, build id, file id).
    /// </summary>
    public ConcurrentDictionary<string, JsonObject> BuildUploads { get; } = new();

    /// <summary>
    /// Builds made from uploads (or added by a test for the fallback mode): id to (app, version, build number, state).
    /// </summary>
    public ConcurrentDictionary<string, JsonObject> IntakeBuilds { get; } = new();

    /// <summary>
    /// Export compliance answers written: build id to value.
    /// </summary>
    public ConcurrentDictionary<string, bool> ExportCompliance { get; } = new();

    private HttpResponseMessage RouteIntake(string method, string path, System.Collections.Specialized.NameValueCollection query, JsonObject body)
    {
        Match m;

        if (method == "POST" && path == "/v1/buildUploads")
        {
            var attributes = body["data"]["attributes"];
            var id = NewId("bu");

            BuildUploads[id] = new JsonObject
            {
                ["app"] = body["data"]["relationships"]["app"]["data"]["id"].GetValue<string>(),
                ["version"] = attributes["cfBundleShortVersionString"].GetValue<string>(),
                ["buildNumber"] = attributes["cfBundleVersion"].GetValue<string>(),
                ["platform"] = attributes["platform"].GetValue<string>(),
                ["state"] = "AWAITING_UPLOAD",
            };

            return Ok(new JsonObject { ["data"] = new JsonObject { ["type"] = "buildUploads", ["id"] = id } }, HttpStatusCode.Created);
        }

        if (method == "POST" && path == "/v1/buildUploadFiles")
        {
            var uploadId = body["data"]["relationships"]["buildUpload"]["data"]["id"].GetValue<string>();
            var size = body["data"]["attributes"]["fileSize"].GetValue<long>();
            var fileId = NewId("buf");
            var first = size / 2;

            BuildUploads[uploadId]["file"] = fileId;
            BuildUploads[uploadId]["size"] = size;

            var operations = new JsonArray(Operation(fileId, 1, 0, first), Operation(fileId, 2, first, size - first));

            return Ok(new JsonObject
            {
                ["data"] = new JsonObject { ["type"] = "buildUploadFiles", ["id"] = fileId, ["attributes"] = new JsonObject { ["uploadOperations"] = operations } },
            }, HttpStatusCode.Created);
        }

        if (method == "PATCH" && (m = Regex.Match(path, "^/v1/buildUploadFiles/([^/]+)$")).Success)
        {
            var fileId = m.Groups[1].Value;
            var upload = BuildUploads.First(u => (string)u.Value["file"] == fileId);
            var bytes = BuildUploadParts.Where(p => p.Key.StartsWith($"/build/{fileId}/", StringComparison.Ordinal)).OrderBy(p => p.Key).SelectMany(p => p.Value).ToArray();
            var checksum = body["data"]["attributes"]["sourceFileChecksums"]["file"]["hash"].GetValue<string>();

            if (bytes.Length != upload.Value["size"].GetValue<long>() || checksum != Convert.ToHexStringLower(MD5.HashData(bytes)))
                return new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("""{"errors":[{"detail":"checksum mismatch"}]}""") };

            var buildId = NewId("build");

            IntakeBuilds[buildId] = new JsonObject
            {
                ["app"] = upload.Value["app"].GetValue<string>(),
                ["version"] = upload.Value["version"].GetValue<string>(),
                ["buildNumber"] = upload.Value["buildNumber"].GetValue<string>(),
                ["processingState"] = "VALID",
            };

            upload.Value["state"] = "COMPLETE";
            upload.Value["build"] = buildId;

            return Ok(new JsonObject { ["data"] = new JsonObject { ["type"] = "buildUploadFiles", ["id"] = fileId } });
        }

        if (method == "GET" && (m = Regex.Match(path, "^/v1/buildUploads/([^/]+)$")).Success)
        {
            if (!BuildUploads.TryGetValue(m.Groups[1].Value, out var upload))
                return NotFound();

            return Ok(new JsonObject
            {
                ["data"] = new JsonObject
                {
                    ["type"] = "buildUploads",
                    ["id"] = m.Groups[1].Value,
                    ["attributes"] = new JsonObject { ["state"] = new JsonObject { ["state"] = upload["state"]?.GetValue<string>() } },
                    ["relationships"] = new JsonObject { ["build"] = Rel("builds", upload["build"]?.GetValue<string>()) },
                },
            });
        }

        if (method == "GET" && path == "/v1/builds" && query["filter[preReleaseVersion.version]"] != null)
        {
            var found = IntakeBuilds.Where(b => b.Value["app"].GetValue<string>() == query["filter[app]"]
                                                && b.Value["buildNumber"].GetValue<string>() == query["filter[version]"]
                                                && b.Value["version"].GetValue<string>() == query["filter[preReleaseVersion.version]"]);

            return Ok(new JsonObject { ["data"] = new JsonArray([.. found.Select(b => (JsonNode)BuildResource(b.Key, b.Value))]) });
        }

        if ((m = Regex.Match(path, "^/v1/builds/([^/]+)$")).Success && IntakeBuilds.TryGetValue(m.Groups[1].Value, out var build))
        {
            if (method == "PATCH")
            {
                ExportCompliance[m.Groups[1].Value] = body["data"]["attributes"]["usesNonExemptEncryption"].GetValue<bool>();
                build["usesNonExemptEncryption"] = ExportCompliance[m.Groups[1].Value];
            }

            return Ok(new JsonObject { ["data"] = BuildResource(m.Groups[1].Value, build) });
        }

        return null;
    }

    private static JsonObject Operation(string fileId, int partNumber, long offset, long length) => new()
    {
        ["method"] = "PUT",
        ["url"] = $"{UploadHost}/build/{fileId}/{partNumber}",
        ["offset"] = offset,
        ["length"] = length,
        ["partNumber"] = partNumber,
        ["requestHeaders"] = new JsonArray(new JsonObject { ["name"] = "Content-Type", ["value"] = "application/octet-stream" }),
    };

    private static JsonObject BuildResource(string id, JsonObject build) => new()
    {
        ["type"] = "builds",
        ["id"] = id,
        ["attributes"] = new JsonObject
        {
            ["version"] = build["buildNumber"]?.GetValue<string>(),
            ["processingState"] = build["processingState"]?.GetValue<string>(),
            ["usesNonExemptEncryption"] = build["usesNonExemptEncryption"]?.DeepClone(),
        },
    };
}
