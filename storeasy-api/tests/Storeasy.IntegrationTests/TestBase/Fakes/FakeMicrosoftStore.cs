using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// An in-memory Microsoft behind the Microsoft Store client's HTTP handler: Entra tokens, the MSIX submission API (apps,
/// app / flight / add-on submissions, commit, status, package rollout, flights, add-ons), the MSI/EXE submission API
/// (status, packages, metadata, submit), Azure blob uploads, storage downloads and installer downloads. Nothing leaves the
/// process. Data is partitioned by tenant (each test uses its own) through the token.
/// </summary>
public sealed partial class FakeMicrosoftStore : HttpMessageHandler
{
    /// <summary>Host of the SAS upload addresses.</summary>
    public const string BlobHost = "blob.fake.test";

    /// <summary>Host of the installer downloads.</summary>
    public const string DownloadHost = "downloads.fake.test";

    private static long _sequence = 1152921504620000000;

    private readonly object _gate = new();

    /// <summary>An owner of submissions (app, flight, add-on).</summary>
    public sealed class Owner
    {
        public string Tenant { get; set; }

        public string Id { get; set; }

        public JsonObject Resource { get; set; } = [];

        public string PendingId { get; set; }

        public string PublishedId { get; set; }
    }

    public ConcurrentDictionary<string, Owner> Apps { get; } = new();

    public ConcurrentDictionary<string, Owner> Flights { get; } = new();

    public ConcurrentDictionary<string, Owner> Addons { get; } = new();

    public ConcurrentDictionary<string, JsonObject> Submissions { get; } = new();

    /// <summary>MSI/EXE products by id: metadata, packages, readiness, submissions.</summary>
    public ConcurrentDictionary<string, JsonObject> Products { get; } = new();

    /// <summary>Paths (relative, without query) that answer 409.</summary>
    public ConcurrentDictionary<string, bool> Conflicts { get; } = new();

    /// <summary>Uploaded ZIP archives by SAS path.</summary>
    public ConcurrentDictionary<string, byte[]> Uploads { get; } = new();

    /// <summary>Installer files by download path.</summary>
    public ConcurrentDictionary<string, byte[]> Installers { get; } = new();

    /// <summary>Every request as "METHOD host/path".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    public static string NewId() => Interlocked.Increment(ref _sequence).ToString();

    /// <summary>
    /// Adds an MSIX app with a published submission.
    /// </summary>
    public Owner AddApp(string tenant, string appId, string name, JsonObject published)
    {
        var app = new Owner { Tenant = tenant, Id = appId };

        app.Resource = new JsonObject { ["id"] = appId, ["primaryName"] = name, ["packageFamilyName"] = $"Fake.{appId}_8wekyb3d8bbwe" };

        if (published != null)
        {
            var id = NewId();

            published["id"] = id;
            published["status"] = "Published";
            Submissions[id] = published;
            app.PublishedId = id;
        }

        Apps[appId] = app;

        return app;
    }

    /// <summary>
    /// Adds a flight with an optional pending submission.
    /// </summary>
    public Owner AddFlight(string appId, string flightId, string name, JsonObject pending)
    {
        var flight = new Owner { Tenant = Apps[appId].Tenant, Id = flightId, Resource = new JsonObject { ["flightId"] = flightId, ["friendlyName"] = name, ["groupIds"] = new JsonArray("1152921504606962205") } };

        if (pending != null)
        {
            var id = NewId();

            pending["id"] = id;
            pending["status"] = "PendingCommit";

            if (pending["applicationPackages"] is JsonNode packages)
            {
                pending.Remove("applicationPackages");
                pending["flightPackages"] = packages;
            }

            Submissions[id] = pending;
            flight.PendingId = id;
        }

        Flights[$"{appId}/{flightId}"] = flight;

        return flight;
    }

    /// <summary>
    /// Adds an add-on with a published submission.
    /// </summary>
    public Owner AddAddon(string appId, string addonId, string productId, string title)
    {
        var addon = new Owner
        {
            Tenant = Apps[appId].Tenant,
            Id = addonId,
            Resource = new JsonObject
            {
                ["id"] = addonId,
                ["productId"] = productId,
                ["productType"] = "Durable",
                ["applications"] = new JsonObject { ["value"] = new JsonArray(new JsonObject { ["id"] = appId }), ["totalCount"] = 1 },
            },
        };

        var id = NewId();

        Submissions[id] = new JsonObject
        {
            ["id"] = id,
            ["status"] = "Published",
            ["lifetime"] = "Forever",
            ["pricing"] = new JsonObject { ["priceId"] = "Tier2" },
            ["listings"] = new JsonObject { ["en-us"] = new JsonObject { ["title"] = title, ["description"] = "An add-on" } },
        };

        addon.PublishedId = id;
        Addons[addonId] = addon;

        return addon;
    }

    /// <summary>
    /// Adds an MSI/EXE product.
    /// </summary>
    public JsonObject AddProduct(string productId, bool ready = true) => Products[productId] = new JsonObject
    {
        ["isReady"] = ready,
        ["ongoingSubmissionId"] = string.Empty,
        ["packages"] = new JsonArray(),
        ["metadata"] = new JsonObject
        {
            ["listings"] = new JsonArray(new JsonObject { ["language"] = "en-us", ["description"] = "Installer app", ["whatsNew"] = string.Empty, ["productFeatures"] = new JsonArray(), ["searchTerms"] = new JsonArray() }),
            ["availability"] = new JsonObject { ["markets"] = new JsonArray("US"), ["discoverability"] = "DISCOVERABLE", ["pricing"] = "FREE" },
        },
        ["submissions"] = new JsonObject(),
    };

    /// <summary>
    /// Changes a submission as a Partner Center user would.
    /// </summary>
    public void EditInPartnerCenter(string submissionId)
    {
        lock (_gate)
            Submissions[submissionId]["notesForCertification"] = $"edited {Guid.NewGuid():N}";
    }

    /// <summary>
    /// A basic app submission.
    /// </summary>
    public static JsonObject AppSubmission(string description, string priceId = "Free", string version = "1.0.0.0") => new()
    {
        ["status"] = "Published",
        ["statusDetails"] = new JsonObject { ["errors"] = new JsonArray(), ["warnings"] = new JsonArray(), ["certificationReports"] = new JsonArray() },
        ["pricing"] = new JsonObject { ["priceId"] = priceId, ["trialPeriod"] = "NoFreeTrial" },
        ["visibility"] = "Public",
        ["listings"] = new JsonObject
        {
            ["en-us"] = new JsonObject
            {
                ["baseListing"] = new JsonObject
                {
                    ["title"] = "Pusula",
                    ["description"] = description,
                    ["features"] = new JsonArray("Maps"),
                    ["keywords"] = new JsonArray("maps"),
                    ["images"] = new JsonArray(new JsonObject { ["fileName"] = "shot1.png", ["fileStatus"] = "Uploaded", ["id"] = "11", ["imageType"] = "Screenshot" }),
                },
                ["platformOverrides"] = new JsonObject(),
            },
        },
        ["applicationPackages"] = new JsonArray(new JsonObject { ["fileName"] = $"app_{version}_x64.msix", ["fileStatus"] = "Uploaded", ["id"] = NewId(), ["version"] = version, ["architecture"] = "x64" }),
        ["packageDeliveryOptions"] = new JsonObject
        {
            ["packageRollout"] = new JsonObject { ["isPackageRollout"] = false, ["packageRolloutPercentage"] = 0, ["packageRolloutStatus"] = "PackageRolloutNotStarted", ["fallbackSubmissionId"] = "0" },
            ["isMandatoryUpdate"] = false,
        },
    };

    /// <summary>
    /// Entry names of the ZIP uploaded to a submission.
    /// </summary>
    public List<string> UploadedEntries(string submissionId)
    {
        if (!Uploads.TryGetValue(submissionId, out var bytes))
            return [];

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        return [.. archive.Entries.Select(e => e.FullName)];
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var uri = request.RequestUri!;

        Calls.Enqueue($"{request.Method} {uri.Host}{uri.AbsolutePath}");

        lock (_gate)
        {
            return uri.Host switch
            {
                "login.microsoftonline.com" => Token(uri),
                "manage.devcenter.microsoft.com" => DevCenter(request, uri, body),
                "api.store.microsoft.com" => StoreApi(request, uri, body),
                BlobHost => Blob(request, uri, body),
                "storage.test" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes($"file {uri.AbsolutePath}")) },
                DownloadHost => Installers.TryGetValue(uri.AbsolutePath, out var file) ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(file) } : new HttpResponseMessage(HttpStatusCode.NotFound),
                _ => new HttpResponseMessage(HttpStatusCode.BadGateway),
            };
        }
    }

    private static HttpResponseMessage Json(JsonNode node, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(node?.ToJsonString() ?? "{}", Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status, string message) => Json(new JsonObject { ["code"] = status.ToString(), ["message"] = message }, status);

    private static HttpResponseMessage Token(Uri uri)
    {
        var tenant = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];

        return Json(new JsonObject { ["access_token"] = $"fake-{tenant}", ["expires_in"] = 3599, ["token_type"] = "Bearer" });
    }

    private static string Tenant(HttpRequestMessage request) => request.Headers.Authorization?.Parameter?.Replace("fake-", string.Empty);

    [GeneratedRegex("^(?<owner>applications/(?<app>[^/]+)(/flights/(?<flight>[^/]+))?|inappproducts/(?<addon>[^/]+))/submissions(/(?<sid>[^/]+))?(/(?<op>[a-z]+))?$")]
    private static partial Regex SubmissionPath();

    private HttpResponseMessage DevCenter(HttpRequestMessage request, Uri uri, byte[] body)
    {
        var path = uri.AbsolutePath["/v1.0/my/".Length..];

        if (Conflicts.ContainsKey(path))
            return Error(HttpStatusCode.Conflict, "The product uses a feature the API does not support.");

        var tenant = Tenant(request);
        var match = SubmissionPath().Match(path);

        if (match.Success)
            return SubmissionCall(request.Method, match, uri, body);

        var parts = path.Split('/');

        if (path == "applications")
        {
            var apps = Apps.Values.Where(a => a.Tenant == tenant).OrderBy(a => a.Id).Select(AppResource).ToList();
            var skip = uri.Query.Contains("skip=1") ? 1 : 0;
            var page = new JsonObject { ["value"] = new JsonArray([.. apps.Skip(skip).Take(skip == 0 ? 1 : int.MaxValue)]), ["totalCount"] = apps.Count };

            if (skip == 0 && apps.Count > 1)
                page["@nextLink"] = "applications?skip=1&top=100";

            return Json(page);
        }

        if (parts[0] == "applications" && parts.Length == 2)
            return Apps.TryGetValue(parts[1], out var app) && app.Tenant == tenant ? Json(AppResource(app)) : Error(HttpStatusCode.NotFound, "Not found");

        if (parts[0] == "applications" && parts.Length == 3 && parts[2] == "listflights")
            return Json(new JsonObject { ["value"] = new JsonArray([.. Flights.Where(f => f.Key.StartsWith($"{parts[1]}/")).Select(f => FlightResource(f.Value))]) });

        if (parts[0] == "applications" && parts.Length == 3 && parts[2] == "flights" && request.Method == HttpMethod.Post)
        {
            var input = JsonNode.Parse(body)!;
            var flight = AddFlight(parts[1], NewId(), input["friendlyName"]!.GetValue<string>(), null);

            flight.Resource["groupIds"] = input["groupIds"]!.DeepClone();

            return Json(FlightResource(flight), HttpStatusCode.Created);
        }

        if (parts[0] == "applications" && parts.Length == 3 && parts[2] == "listinappproducts")
            return Json(new JsonObject { ["value"] = new JsonArray([.. Addons.Values.Where(a => a.Resource["applications"]!["value"]![0]!["id"]!.GetValue<string>() == parts[1]).Select(a => (JsonNode)new JsonObject { ["inAppProductId"] = a.Id })]) });

        if (path == "inappproducts" && request.Method == HttpMethod.Post)
        {
            var input = JsonNode.Parse(body)!;
            var addonId = $"9N{Random.Shared.Next(100000000, 999999999)}X";
            var addon = new Owner
            {
                Tenant = tenant,
                Id = addonId,
                Resource = new JsonObject
                {
                    ["id"] = addonId,
                    ["productId"] = input["productId"]!.GetValue<string>(),
                    ["productType"] = input["productType"]!.GetValue<string>(),
                    ["applications"] = new JsonObject { ["value"] = new JsonArray(new JsonObject { ["id"] = input["applicationIds"]![0]!.GetValue<string>() }) },
                },
            };

            Addons[addonId] = addon;

            return Json(AddonResource(addon), HttpStatusCode.Created);
        }

        if (parts[0] == "inappproducts" && parts.Length == 2)
            return Addons.TryGetValue(parts[1], out var addon) ? Json(AddonResource(addon)) : Error(HttpStatusCode.NotFound, "Not found");

        return Error(HttpStatusCode.NotFound, $"No route {path}");
    }

    private Owner OwnerOf(Match match)
    {
        if (match.Groups["addon"].Success)
            return Addons.GetValueOrDefault(match.Groups["addon"].Value);

        if (match.Groups["flight"].Success)
            return Flights.GetValueOrDefault($"{match.Groups["app"].Value}/{match.Groups["flight"].Value}");

        return Apps.GetValueOrDefault(match.Groups["app"].Value);
    }

    private HttpResponseMessage SubmissionCall(HttpMethod method, Match match, Uri uri, byte[] body)
    {
        var owner = OwnerOf(match);

        if (owner == null)
            return Error(HttpStatusCode.NotFound, "Owner not found");

        var sid = match.Groups["sid"].Success ? match.Groups["sid"].Value : null;
        var op = match.Groups["op"].Success ? match.Groups["op"].Value : null;

        if (sid == null && method == HttpMethod.Post)
        {
            if (owner.PendingId != null)
                return Error(HttpStatusCode.Conflict, "Only one pending submission is allowed.");

            var id = NewId();
            var copy = owner.PublishedId != null ? Submissions[owner.PublishedId].DeepClone().AsObject() : new JsonObject { ["listings"] = new JsonObject() };

            copy["id"] = id;
            copy["status"] = "PendingCommit";
            copy["fileUploadUrl"] = $"https://{BlobHost}/ingestion/{id}?sv=2014-02-14&sr=b&sig=FAKESIG";
            Submissions[id] = copy;
            owner.PendingId = id;

            return Json(copy, HttpStatusCode.Created);
        }

        if (sid == null || !Submissions.TryGetValue(sid, out var submission))
            return Error(HttpStatusCode.NotFound, "Submission not found");

        var status = submission["status"]?.GetValue<string>();

        switch (op)
        {
            case null when method == HttpMethod.Get:
                return Json(submission);
            case null when method == HttpMethod.Put:
            {
                if (status != "PendingCommit")
                    return Error(HttpStatusCode.Conflict, "Only drafts can be changed.");

                var content = JsonNode.Parse(body)!.AsObject();

                content["id"] = sid;
                content["status"] = status;
                content["fileUploadUrl"] = submission["fileUploadUrl"]?.DeepClone();
                Submissions[sid] = content;

                return Json(content);
            }
            case null when method == HttpMethod.Delete:
                Submissions.TryRemove(sid, out _);

                if (owner.PendingId == sid)
                    owner.PendingId = null;

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            case "commit":
                if (status != "PendingCommit")
                    return Error(HttpStatusCode.Conflict, "Not a draft.");

                submission["status"] = "CommitStarted";

                return Json(new JsonObject { ["status"] = "CommitStarted" });
            case "status":
                if (status == "CommitStarted")
                    submission["status"] = status = "PreProcessing";

                return Json(new JsonObject { ["status"] = status, ["statusDetails"] = submission["statusDetails"]?.DeepClone() ?? new JsonObject() });
            case "packagerollout":
                return Json(submission["packageDeliveryOptions"]?["packageRollout"]?.DeepClone() ?? new JsonObject());
            default:
            {
                var rollout = submission["packageDeliveryOptions"]!["packageRollout"]!.AsObject();

                if (op == "updatepackagerolloutpercentage")
                    rollout["packageRolloutPercentage"] = double.Parse(Regex.Match(uri.Query, "percentage=([0-9.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                else
                    rollout["packageRolloutStatus"] = op == "haltpackagerollout" ? "PackageRolloutStopped" : "PackageRolloutComplete";

                if (op == "finalizepackagerollout")
                    rollout["packageRolloutPercentage"] = 100;

                return Json(rollout.DeepClone());
            }
        }
    }

    /// <summary>
    /// Publishes a submission (certification passed) and makes it the owner's last published one.
    /// </summary>
    public void Publish(string submissionId, bool rollout = false)
    {
        lock (_gate)
        {
            var submission = Submissions[submissionId];

            submission["status"] = "Published";

            if (rollout)
                submission["packageDeliveryOptions"]!["packageRollout"] = new JsonObject { ["isPackageRollout"] = true, ["packageRolloutPercentage"] = 10.0, ["packageRolloutStatus"] = "PackageRolloutInProgress", ["fallbackSubmissionId"] = "0" };

            foreach (var owner in Apps.Values.Concat(Flights.Values).Concat(Addons.Values).Where(o => o.PendingId == submissionId))
            {
                owner.PendingId = null;
                owner.PublishedId = submissionId;
            }
        }
    }

    private static JsonObject Ref(string id) => id == null ? null : new JsonObject { ["id"] = id, ["resourceLocation"] = $"submissions/{id}" };

    private static JsonNode AppResource(Owner app)
    {
        var resource = app.Resource.DeepClone().AsObject();

        resource["lastPublishedApplicationSubmission"] = Ref(app.PublishedId);
        resource["pendingApplicationSubmission"] = Ref(app.PendingId);

        return resource;
    }

    private static JsonNode FlightResource(Owner flight)
    {
        var resource = flight.Resource.DeepClone().AsObject();

        resource["lastPublishedFlightSubmission"] = Ref(flight.PublishedId);
        resource["pendingFlightSubmission"] = Ref(flight.PendingId);

        return resource;
    }

    private static JsonNode AddonResource(Owner addon)
    {
        var resource = addon.Resource.DeepClone().AsObject();

        resource["lastPublishedInAppProductSubmission"] = Ref(addon.PublishedId);
        resource["pendingInAppProductSubmission"] = Ref(addon.PendingId);

        return resource;
    }

    private HttpResponseMessage StoreApi(HttpRequestMessage request, Uri uri, byte[] body)
    {
        if (!request.Headers.Contains("X-Seller-Account-Id"))
            return Error(HttpStatusCode.BadRequest, "Missing seller id.");

        var match = Regex.Match(uri.AbsolutePath, "^/submission/v1/product/(?<id>[^/]+)/(?<tail>.+)$");

        if (!match.Success || !Products.TryGetValue(match.Groups["id"].Value, out var product))
            return Json(new JsonObject { ["isSuccess"] = false, ["errors"] = new JsonArray(new JsonObject { ["code"] = "NotFound", ["message"] = "Product not found" }) }, HttpStatusCode.NotFound);

        var tail = match.Groups["tail"].Value;

        JsonNode data;

        switch (tail)
        {
            case "status":
                data = new JsonObject { ["isReady"] = product["isReady"]!.DeepClone(), ["ongoingSubmissionId"] = product["ongoingSubmissionId"]!.DeepClone() };
                break;
            case "packages" when request.Method == HttpMethod.Get:
                data = new JsonObject { ["packages"] = product["packages"]!.DeepClone() };
                break;
            case "packages" when request.Method == HttpMethod.Put:
            {
                var packages = JsonNode.Parse(body)!["packages"]!.AsArray();

                foreach (var package in packages)
                    package!["packageId"] = NewId();

                product["packages"] = packages.DeepClone();
                product["isReady"] = false;
                data = new JsonObject();
                break;
            }
            case "packages/commit":
                product["isReady"] = true;
                data = new JsonObject { ["pollingUrl"] = $"/submission/v1/product/{match.Groups["id"].Value}/status" };
                break;
            case "metadata" when request.Method == HttpMethod.Get:
                data = product["metadata"]!.DeepClone();
                break;
            case "metadata":
            {
                var patch = JsonNode.Parse(body)!.AsObject();
                var metadata = product["metadata"]!.AsObject();

                if (patch["listings"] is JsonObject listing)
                {
                    var listings = metadata["listings"]!.AsArray();
                    var existing = listings.FirstOrDefault(l => l!["language"]!.GetValue<string>() == listing["language"]!.GetValue<string>());

                    if (existing == null)
                    {
                        listings.Add(listing.DeepClone());
                    }
                    else
                    {
                        foreach (var property in listing)
                            existing[property.Key] = property.Value?.DeepClone();
                    }
                }

                if (patch["availability"] is JsonObject availability)
                {
                    foreach (var property in availability)
                        metadata["availability"]![property.Key] = property.Value?.DeepClone();
                }

                data = new JsonObject();
                break;
            }
            case "submit":
            {
                var id = NewId();

                product["ongoingSubmissionId"] = id;
                product["submissions"]![id] = "INPROGRESS";
                data = new JsonObject { ["submissionId"] = id, ["ongoingSubmissionId"] = id };
                break;
            }
            default:
            {
                var status = Regex.Match(tail, "^submission/(?<sid>[^/]+)/status$");

                if (!status.Success)
                    return Error(HttpStatusCode.NotFound, "No route");

                data = new JsonObject { ["publishingStatus"] = product["submissions"]![status.Groups["sid"].Value]?.DeepClone() ?? "UNKNOWN", ["hasFailed"] = false };
                break;
            }
        }

        return Json(new JsonObject { ["isSuccess"] = true, ["errors"] = new JsonArray(), ["responseData"] = data });
    }

    private HttpResponseMessage Blob(HttpRequestMessage request, Uri uri, byte[] body)
    {
        if (request.Method != HttpMethod.Put || !uri.Query.Contains("sig=") || !request.Headers.TryGetValues("x-ms-blob-type", out var type) || type.Single() != "BlockBlob")
            return new HttpResponseMessage(HttpStatusCode.Forbidden);

        Uploads[uri.AbsolutePath.Split('/')[^1]] = body;

        return new HttpResponseMessage(HttpStatusCode.Created);
    }
}
