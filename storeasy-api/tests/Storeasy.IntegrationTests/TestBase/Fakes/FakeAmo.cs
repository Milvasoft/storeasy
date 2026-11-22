using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// An in-memory addons.mozilla.org API v5 behind the AMO client's HTTP handler: JWT checked against the account's secret
/// (HS256, at most five minutes), the account's add-ons, add-on detail and edit (texts per locale, categories, icon), versions
/// (listed versions wait for review, unlisted ones are signed at once), uploads (manifest.json read from the package and
/// validated on the next read), previews, ratings and developer replies. Tests move a version through the review with
/// <see cref="Approve"/> and <see cref="Reject"/>. Nothing leaves the process; each test uses its own account.
/// </summary>
public sealed class FakeAmo : HttpMessageHandler
{
    /// <summary>API base address the tests configure.</summary>
    public const string ApiBaseUrl = "https://amo.fake/api/v5";

    private static long _nextId = 1000;

    /// <summary>An API key and its account.</summary>
    public sealed record Account(string Issuer, string Secret, long UserId, string Username);

    /// <summary>A version.</summary>
    public sealed class Version
    {
        public long Id { get; set; }

        public string Number { get; set; }

        public string Channel { get; set; }

        public string FileStatus { get; set; }

        public Dictionary<string, string> ReleaseNotes { get; set; } = [];

        public string ApprovalNotes { get; set; }

        public string License { get; set; }

        public string SourceFileName { get; set; }

        public List<string> Compatibility { get; set; } = [];
    }

    /// <summary>A preview.</summary>
    public sealed record Preview(long Id, string FileName, int Position);

    /// <summary>An add-on.</summary>
    public sealed class Addon
    {
        public long Id { get; set; }

        public string Guid { get; set; }

        public string Slug { get; set; }

        public string Status { get; set; } = "public";

        public HashSet<long> Authors { get; } = [];

        public Dictionary<string, string> Name { get; set; } = [];

        public Dictionary<string, string> Summary { get; set; } = [];

        public Dictionary<string, string> Description { get; set; } = [];

        public Dictionary<string, string> Homepage { get; set; } = [];

        public Dictionary<string, string> SupportUrl { get; set; } = [];

        public Dictionary<string, string> SupportEmail { get; set; } = [];

        public List<string> Categories { get; set; } = ["tabs"];

        public List<Preview> Previews { get; } = [];

        public string IconFileName { get; set; }

        public List<Version> Versions { get; } = [];

        public List<JsonObject> Ratings { get; } = [];

        public long AverageDailyUsers { get; set; }

        public long WeeklyDownloads { get; set; }

        public List<JsonObject> Patches { get; } = [];
    }

    /// <summary>An upload.</summary>
    public sealed class Upload
    {
        public string Uuid { get; set; }

        public long UserId { get; set; }

        public string Channel { get; set; }

        public string Version { get; set; }

        public string Guid { get; set; }

        public bool Valid { get; set; }

        public bool Processed { get; set; }

        public bool Submitted { get; set; }

        public string Error { get; set; }
    }

    private readonly object _gate = new();

    /// <summary>Accounts by issuer.</summary>
    public ConcurrentDictionary<string, Account> Accounts { get; } = new();

    /// <summary>Add-ons by id.</summary>
    public ConcurrentDictionary<long, Addon> Addons { get; } = new();

    /// <summary>Uploads by uuid.</summary>
    public ConcurrentDictionary<string, Upload> Uploads { get; } = new();

    /// <summary>Every call: "METHOD path".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>
    /// A new account with an API key.
    /// </summary>
    public Account AddAccount()
    {
        var userId = Interlocked.Increment(ref _nextId);
        var account = new Account($"user:{userId}:{userId % 97}", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), userId, $"dev{userId}");

        Accounts[account.Issuer] = account;

        return account;
    }

    /// <summary>
    /// A new approved add-on of <paramref name="account"/> with one approved listed version.
    /// </summary>
    public Addon AddAddon(Account account, string version = "1.0.0", string name = "Kabuk Tabs")
    {
        var id = Interlocked.Increment(ref _nextId);
        var addon = new Addon
        {
            Id = id,
            Guid = $"kabuk-{id}@milvasoft.com",
            Slug = $"kabuk-tabs-{id}",
            Name = new() { ["en-US"] = name, ["tr"] = $"{name} TR" },
            Summary = new() { ["en-US"] = "Tabs that stay tidy." },
            Description = new() { ["en-US"] = "A long description." },
            Homepage = new() { ["en-US"] = "https://milvasoft.com/kabuk" },
            AverageDailyUsers = 1200,
            WeeklyDownloads = 300,
        };

        addon.Authors.Add(account.UserId);

        if (version != null)
            addon.Versions.Add(new Version { Id = Interlocked.Increment(ref _nextId), Number = version, Channel = "listed", FileStatus = "public", License = "MPL-2.0", ReleaseNotes = new() { ["en-US"] = $"Notes of {version}" } });

        Addons[id] = addon;

        return addon;
    }

    /// <summary>
    /// Approves <paramref name="version"/>: its file becomes public, the add-on approved.
    /// </summary>
    public void Approve(long addonId, string version)
    {
        lock (_gate)
        {
            var addon = Addons[addonId];

            addon.Versions.Single(v => v.Number == version).FileStatus = "public";
            addon.Status = "public";
        }
    }

    /// <summary>
    /// Rejects <paramref name="version"/>: its file is disabled.
    /// </summary>
    public void Reject(long addonId, string version)
    {
        lock (_gate)
        {
            Addons[addonId].Versions.Single(v => v.Number == version).FileStatus = "disabled";
        }
    }

    /// <summary>
    /// A Firefox extension package: manifest.json with the version and add-on id, a background script (one long line when
    /// <paramref name="minified"/>).
    /// </summary>
    public static byte[] Xpi(string version, string guid, int manifestVersion = 3, bool dataCollection = true, bool minified = false)
    {
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var gecko = new JsonObject { ["id"] = guid, ["strict_min_version"] = "128.0" };

            if (dataCollection)
                gecko["data_collection_permissions"] = new JsonObject { ["required"] = new JsonArray("none") };

            var manifest = new JsonObject
            {
                ["manifest_version"] = manifestVersion,
                ["name"] = "Kabuk Tabs",
                ["version"] = version,
                ["description"] = "Tabs that stay tidy.",
                ["icons"] = new JsonObject { ["48"] = "icon.png" },
                ["background"] = new JsonObject { ["scripts"] = new JsonArray("background.js") },
                ["permissions"] = new JsonArray("tabs", "storage"),
            };

            if (guid != null)
                manifest["browser_specific_settings"] = new JsonObject { ["gecko"] = gecko };

            Write(archive, "manifest.json", manifest.ToJsonString());
            Write(archive, "background.js", minified ? "var a=1;" + string.Concat(Enumerable.Repeat("function b(c){return c+1}", 80)) : "// Tabs.\nbrowser.tabs.query({});\n");
        }

        return memory.ToArray();
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        using var stream = archive.CreateEntry(name).Open();
        var bytes = Encoding.UTF8.GetBytes(text);

        stream.Write(bytes);
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        Calls.Enqueue($"{request.Method} {path}");

        if (!path.StartsWith("/api/v5/", StringComparison.Ordinal))
            return Status(HttpStatusCode.NotFound, "Not found.");

        var account = Authenticate(request);

        if (account == null)
            return Status(HttpStatusCode.Unauthorized, "Incorrect authentication credentials.");

        var route = path["/api/v5".Length..];
        var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
        var segments = route.Trim('/').Split('/');

        try
        {
            return (request.Method.Method, segments) switch
            {
                ("GET", ["accounts", "profile"]) => Json(new JsonObject { ["id"] = account.UserId, ["username"] = account.Username, ["name"] = $"Developer {account.UserId}" }),
                ("GET", ["addons", "addon"]) => Json(Page([.. Addons.Values.Where(a => a.Authors.Contains(account.UserId)).OrderBy(a => a.Id).Select(AddonJson)])),
                ("GET", ["addons", "addon", var reference]) => Find(reference) is { } addon ? Json(AddonJson(addon)) : Status(HttpStatusCode.NotFound, "Not found."),
                ("PATCH", ["addons", "addon", var reference]) => await PatchAddonAsync(account, reference, request.Content, cancellationToken),
                ("GET", ["addons", "addon", var reference, "versions"]) => Versions(account, reference, query["filter"]),
                ("POST", ["addons", "addon", var reference, "versions"]) => await CreateVersionAsync(account, reference, request.Content, cancellationToken),
                ("PATCH", ["addons", "addon", var reference, "versions", var versionId]) => await PatchVersionAsync(account, reference, versionId, request.Content, cancellationToken),
                ("POST", ["addons", "addon", var reference, "previews"]) => await AddPreviewAsync(account, reference, request.Content, cancellationToken),
                ("DELETE", ["addons", "addon", var reference, "previews", var previewId]) => DeletePreview(account, reference, previewId),
                ("POST", ["addons", "upload"]) => await UploadAsync(account, request.Content, cancellationToken),
                ("GET", ["addons", "upload", var uuid]) => GetUpload(account, uuid),
                ("GET", ["ratings", "rating"]) => Ratings(query["addon"]),
                ("POST", ["ratings", "rating", var ratingId, "reply"]) => await ReplyAsync(account, ratingId, request.Content, cancellationToken),
                _ => Status(HttpStatusCode.NotFound, "Not found."),
            };
        }
        catch (InvalidOperationException ex)
        {
            return Status(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    private Account Authenticate(HttpRequestMessage request)
    {
        if (request.Headers.Authorization is not { Scheme: "JWT", Parameter: { } token })
            return null;

        var parts = token.Split('.');

        if (parts.Length != 3)
            return null;

        var payload = JsonNode.Parse(Encoding.UTF8.GetString(Base64Url(parts[1])));
        var issuer = payload?["iss"]?.GetValue<string>();

        if (issuer == null || !Accounts.TryGetValue(issuer, out var account))
            return null;

        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(account.Secret), Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"));

        if (!CryptographicOperations.FixedTimeEquals(signature, Base64Url(parts[2])))
            return null;

        var issuedAt = payload["iat"]!.GetValue<long>();
        var expires = payload["exp"]!.GetValue<long>();

        return expires - issuedAt > 300 || payload["jti"] == null ? null : account;
    }

    private static byte[] Base64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }

    private Addon Find(string reference)
        => Addons.Values.FirstOrDefault(a => a.Id.ToString(CultureInfo.InvariantCulture) == reference || a.Guid == reference || a.Slug == reference);

    private Addon Owned(Account account, string reference)
        => Find(reference) is { } addon && addon.Authors.Contains(account.UserId) ? addon : throw new InvalidOperationException("You do not have permission to perform this action.");

    private JsonObject AddonJson(Addon addon)
    {
        lock (_gate)
        {
            var current = addon.Versions.Where(v => v.Channel == "listed" && v.FileStatus == "public").OrderByDescending(v => v.Id).FirstOrDefault();
            var ratings = addon.Ratings.Where(r => r["is_developer_reply"]?.GetValue<bool>() != true).ToList();

            return new JsonObject
            {
                ["id"] = addon.Id,
                ["guid"] = addon.Guid,
                ["slug"] = addon.Slug,
                ["type"] = "extension",
                ["status"] = addon.Status,
                ["is_disabled"] = false,
                ["default_locale"] = "en-US",
                ["name"] = Translated(addon.Name),
                ["summary"] = Translated(addon.Summary),
                ["description"] = Translated(addon.Description),
                ["homepage"] = new JsonObject { ["url"] = Translated(addon.Homepage), ["outgoing"] = Translated(addon.Homepage) },
                ["support_url"] = new JsonObject { ["url"] = Translated(addon.SupportUrl), ["outgoing"] = Translated(addon.SupportUrl) },
                ["support_email"] = Translated(addon.SupportEmail),
                ["categories"] = new JsonArray([.. addon.Categories.Select(c => (JsonNode)c)]),
                ["icon_url"] = addon.IconFileName == null ? "https://addons.cdn.fake/default-64.png" : $"https://addons.cdn.fake/icons/{addon.Id}/{addon.IconFileName}",
                ["previews"] = new JsonArray([.. addon.Previews.OrderBy(p => p.Position).Select(p => (JsonNode)new JsonObject
                {
                    ["id"] = p.Id,
                    ["caption"] = null,
                    ["image_url"] = $"https://addons.cdn.fake/previews/{p.Id}/{p.FileName}",
                    ["thumbnail_url"] = $"https://addons.cdn.fake/previews/thumbs/{p.Id}.png",
                    ["image_size"] = new JsonArray(1280, 800),
                    ["position"] = p.Position,
                })]),
                ["current_version"] = current == null ? null : VersionJson(current),
                ["ratings"] = new JsonObject { ["count"] = ratings.Count, ["average"] = ratings.Count == 0 ? 0 : ratings.Average(r => r["score"]!.GetValue<int>()) },
                ["average_daily_users"] = addon.AverageDailyUsers,
                ["weekly_downloads"] = addon.WeeklyDownloads,
                ["url"] = $"https://addons.fake/firefox/addon/{addon.Slug}/",
                ["edit_url"] = $"https://addons.fake/developers/addon/{addon.Slug}/edit",
            };
        }
    }

    private static JsonObject VersionJson(Version version) => new()
    {
        ["id"] = version.Id,
        ["version"] = version.Number,
        ["channel"] = version.Channel,
        ["is_disabled"] = false,
        ["release_notes"] = Translated(version.ReleaseNotes),
        ["license"] = version.License == null ? null : new JsonObject { ["slug"] = version.License, ["name"] = new JsonObject { ["en-US"] = version.License } },
        ["source"] = version.SourceFileName == null ? null : $"https://addons.fake/source/{version.Id}/{version.SourceFileName}",
        ["compatibility"] = new JsonObject { ["firefox"] = new JsonObject { ["min"] = "128.0", ["max"] = "*" } },
        ["file"] = new JsonObject
        {
            ["id"] = version.Id + 1,
            ["created"] = "2026-11-01T10:00:00Z",
            ["status"] = version.FileStatus,
            ["url"] = $"https://addons.fake/files/{version.Id}.xpi",
            ["size"] = 1024,
            ["permissions"] = new JsonArray("tabs"),
            ["data_collection_permissions"] = new JsonArray("none"),
        },
    };

    private static JsonObject Translated(Dictionary<string, string> texts)
    {
        var obj = new JsonObject();

        foreach (var (locale, text) in texts)
            obj[locale] = text;

        return obj;
    }

    private static JsonObject Page(List<JsonObject> results)
        => new() { ["count"] = results.Count, ["next"] = null, ["previous"] = null, ["page_size"] = 50, ["page_count"] = 1, ["results"] = new JsonArray([.. results]) };

    private HttpResponseMessage Versions(Account account, string reference, string filter)
    {
        var addon = Find(reference);

        if (addon == null)
            return Status(HttpStatusCode.NotFound, "Not found.");

        lock (_gate)
        {
            var all = filter == "all_with_unlisted" && addon.Authors.Contains(account.UserId);

            return Json(Page([.. addon.Versions.Where(v => all || (v.Channel == "listed" && v.FileStatus == "public")).OrderByDescending(v => v.Id).Select(VersionJson)]));
        }
    }

    private async Task<HttpResponseMessage> PatchAddonAsync(Account account, string reference, HttpContent content, CancellationToken cancellationToken)
    {
        var addon = Owned(account, reference);

        if (content is MultipartFormDataContent form)
        {
            var icon = form.FirstOrDefault(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "icon") ?? throw new InvalidOperationException("icon: no file.");

            addon.IconFileName = icon.Headers.ContentDisposition!.FileName?.Trim('"');
            await icon.ReadAsByteArrayAsync(cancellationToken);

            return Json(AddonJson(addon));
        }

        var body = JsonNode.Parse(await content!.ReadAsStringAsync(cancellationToken))!.AsObject();

        lock (_gate)
        {
            addon.Patches.Add(body.DeepClone().AsObject());

            foreach (var (name, value) in body)
            {
                var target = name switch
                {
                    "name" => addon.Name,
                    "summary" => addon.Summary,
                    "description" => addon.Description,
                    "homepage" => addon.Homepage,
                    "support_url" => addon.SupportUrl,
                    "support_email" => addon.SupportEmail,
                    _ => null,
                };

                if (name == "categories")
                {
                    addon.Categories = [.. value!.AsArray().Select(c => c!.GetValue<string>())];
                    continue;
                }

                if (target == null)
                    throw new InvalidOperationException($"{name}: not editable.");

                foreach (var (locale, text) in value!.AsObject())
                {
                    if (text == null)
                        target.Remove(locale);
                    else
                        target[locale] = text.GetValue<string>();
                }
            }
        }

        return Json(AddonJson(addon));
    }

    private async Task<HttpResponseMessage> UploadAsync(Account account, HttpContent content, CancellationToken cancellationToken)
    {
        var form = content as MultipartFormDataContent ?? throw new InvalidOperationException("upload: multipart expected.");
        var file = form.First(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "upload");
        var channel = await form.First(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "channel").ReadAsStringAsync(cancellationToken);
        var bytes = await file.ReadAsByteArrayAsync(cancellationToken);
        var upload = new Upload { Uuid = Guid.NewGuid().ToString("N"), UserId = account.UserId, Channel = channel };

        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
            var manifest = JsonNode.Parse(await reader.ReadToEndAsync(cancellationToken))!;

            upload.Version = manifest["version"]?.GetValue<string>();
            upload.Guid = manifest["browser_specific_settings"]?["gecko"]?["id"]?.GetValue<string>();
            upload.Valid = upload.Version != null && upload.Guid != null;
            upload.Error = upload.Valid ? null : "The add-on ID is missing.";
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or NullReferenceException)
        {
            upload.Error = "The package is not a valid extension.";
        }

        Uploads[upload.Uuid] = upload;

        // Validation runs in the background: the first read after the upload shows it processed.
        return Json(UploadJson(upload));
    }

    private HttpResponseMessage GetUpload(Account account, string uuid)
    {
        if (!Uploads.TryGetValue(uuid, out var upload) || upload.UserId != account.UserId)
            return Status(HttpStatusCode.NotFound, "Not found.");

        upload.Processed = true;

        return Json(UploadJson(upload));
    }

    private static JsonObject UploadJson(Upload upload) => new()
    {
        ["uuid"] = upload.Uuid,
        ["channel"] = upload.Channel,
        ["processed"] = upload.Processed,
        ["submitted"] = upload.Submitted,
        ["valid"] = upload.Processed && upload.Valid,
        ["version"] = upload.Version,
        ["url"] = $"https://addons.fake/upload/{upload.Uuid}",
        ["validation"] = !upload.Processed ? null : new JsonObject
        {
            ["errors"] = upload.Valid ? 0 : 1,
            ["warnings"] = 1,
            ["notices"] = 0,
            ["messages"] = new JsonArray([.. new[] { upload.Error == null ? null : new JsonObject { ["type"] = "error", ["message"] = upload.Error }, new JsonObject { ["type"] = "warning", ["message"] = "<code>innerHTML</code> is unsafe." } }.Where(m => m != null).Select(m => (JsonNode)m)]),
        },
    };

    private async Task<HttpResponseMessage> CreateVersionAsync(Account account, string reference, HttpContent content, CancellationToken cancellationToken)
    {
        var addon = Owned(account, reference);
        var body = JsonNode.Parse(await content!.ReadAsStringAsync(cancellationToken))!.AsObject();
        var uuid = body["upload"]!.GetValue<string>();

        if (!Uploads.TryGetValue(uuid, out var upload) || upload.UserId != account.UserId || !upload.Processed)
            throw new InvalidOperationException("upload: not found or not validated.");

        if (!upload.Valid)
            throw new InvalidOperationException("upload: the upload did not pass validation.");

        if (upload.Submitted)
            throw new InvalidOperationException("upload: this upload has already been submitted.");

        if (upload.Guid != addon.Guid)
            throw new InvalidOperationException("upload: the add-on ID in the manifest does not match.");

        lock (_gate)
        {
            if (addon.Versions.Any(v => v.Number == upload.Version))
                throw new InvalidOperationException($"version: Version {upload.Version} already exists.");

            if (upload.Channel == "listed" && !addon.Versions.Any(v => v.Channel == "listed") && body["license"] == null)
                throw new InvalidOperationException("license: This field is required for listed versions.");

            var version = new Version
            {
                Id = Interlocked.Increment(ref _nextId),
                Number = upload.Version,
                Channel = upload.Channel,
                FileStatus = upload.Channel == "listed" ? "unreviewed" : "public",
                ApprovalNotes = body["approval_notes"]?.GetValue<string>(),
                License = body["license"]?.GetValue<string>() ?? addon.Versions.LastOrDefault()?.License,
                Compatibility = [.. (body["compatibility"] as JsonArray ?? []).Select(a => a!.GetValue<string>())],
            };

            foreach (var (locale, text) in body["release_notes"]?.AsObject() ?? [])
                version.ReleaseNotes[locale] = text!.GetValue<string>();

            upload.Submitted = true;
            addon.Versions.Add(version);

            if (upload.Channel == "listed")
                addon.Status = addon.Status == "public" ? "public" : "nominated";

            return Json(VersionJson(version));
        }
    }

    private async Task<HttpResponseMessage> PatchVersionAsync(Account account, string reference, string versionId, HttpContent content, CancellationToken cancellationToken)
    {
        var addon = Owned(account, reference);
        var version = addon.Versions.FirstOrDefault(v => v.Id.ToString(CultureInfo.InvariantCulture) == versionId) ?? throw new InvalidOperationException("Not found.");

        if (content is MultipartFormDataContent form)
        {
            var source = form.First(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "source");

            await source.ReadAsByteArrayAsync(cancellationToken);
            version.SourceFileName = source.Headers.ContentDisposition!.FileName?.Trim('"');

            return Json(VersionJson(version));
        }

        var body = JsonNode.Parse(await content!.ReadAsStringAsync(cancellationToken))!.AsObject();

        lock (_gate)
        {
            foreach (var (locale, text) in body["release_notes"]?.AsObject() ?? [])
            {
                if (text == null)
                    version.ReleaseNotes.Remove(locale);
                else
                    version.ReleaseNotes[locale] = text.GetValue<string>();
            }

            if (body["approval_notes"] is JsonNode notes)
                version.ApprovalNotes = notes.GetValue<string>();
        }

        return Json(VersionJson(version));
    }

    private async Task<HttpResponseMessage> AddPreviewAsync(Account account, string reference, HttpContent content, CancellationToken cancellationToken)
    {
        var addon = Owned(account, reference);
        var form = content as MultipartFormDataContent ?? throw new InvalidOperationException("image: multipart expected.");
        var image = form.First(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "image");

        await image.ReadAsByteArrayAsync(cancellationToken);

        lock (_gate)
        {
            var preview = new Preview(Interlocked.Increment(ref _nextId), image.Headers.ContentDisposition!.FileName?.Trim('"'), addon.Previews.Count == 0 ? 0 : addon.Previews.Max(p => p.Position) + 1);

            addon.Previews.Add(preview);

            return Json(new JsonObject { ["id"] = preview.Id, ["image_url"] = $"https://addons.cdn.fake/previews/{preview.Id}/{preview.FileName}", ["position"] = preview.Position });
        }
    }

    private HttpResponseMessage DeletePreview(Account account, string reference, string previewId)
    {
        var addon = Owned(account, reference);

        lock (_gate)
        {
            return addon.Previews.RemoveAll(p => p.Id.ToString(CultureInfo.InvariantCulture) == previewId) > 0
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : Status(HttpStatusCode.NotFound, "Not found.");
        }
    }

    private HttpResponseMessage Ratings(string reference)
    {
        var addon = Find(reference ?? string.Empty);

        if (addon == null)
            return Status(HttpStatusCode.BadRequest, "addon: invalid.");

        lock (_gate)
        {
            var ratings = addon.Ratings.Where(r => r["is_developer_reply"]?.GetValue<bool>() != true)
                                       .Select(r =>
                                       {
                                           var copy = r.DeepClone().AsObject();
                                           var reply = addon.Ratings.FirstOrDefault(x => x["reply_to"]?.GetValue<long>() == r["id"]!.GetValue<long>());

                                           copy["reply"] = reply == null ? null : new JsonObject { ["id"] = reply["id"]!.GetValue<long>(), ["body"] = reply["body"]!.GetValue<string>(), ["created"] = reply["created"]!.GetValue<string>() };

                                           return copy;
                                       })
                                       .ToList();

            return Json(Page(ratings));
        }
    }

    /// <summary>
    /// A rating of <paramref name="addon"/>.
    /// </summary>
    public JsonObject AddRating(Addon addon, int score, string body, string user)
    {
        var rating = new JsonObject
        {
            ["id"] = Interlocked.Increment(ref _nextId),
            ["score"] = score,
            ["body"] = body,
            ["created"] = "2026-11-20T10:00:00Z",
            ["user"] = new JsonObject { ["id"] = 7, ["name"] = user },
            ["version"] = new JsonObject { ["id"] = 1, ["version"] = "1.0.0" },
            ["is_developer_reply"] = false,
        };

        lock (_gate)
        {
            addon.Ratings.Add(rating);
        }

        return rating;
    }

    private async Task<HttpResponseMessage> ReplyAsync(Account account, string ratingId, HttpContent content, CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(await content!.ReadAsStringAsync(cancellationToken))!["body"]!.GetValue<string>();

        lock (_gate)
        {
            var addon = Addons.Values.FirstOrDefault(a => a.Ratings.Any(r => r["id"]!.GetValue<long>().ToString(CultureInfo.InvariantCulture) == ratingId)) ?? throw new InvalidOperationException("Not found.");

            if (!addon.Authors.Contains(account.UserId))
                return Status(HttpStatusCode.Forbidden, "You do not have permission to perform this action.");

            var id = long.Parse(ratingId, CultureInfo.InvariantCulture);
            var reply = addon.Ratings.FirstOrDefault(r => r["reply_to"]?.GetValue<long>() == id);

            if (reply == null)
            {
                reply = new JsonObject { ["id"] = Interlocked.Increment(ref _nextId), ["reply_to"] = id, ["is_developer_reply"] = true, ["created"] = "2026-11-21T09:00:00Z" };
                addon.Ratings.Add(reply);
            }

            reply["body"] = body;

            return Json(new JsonObject { ["id"] = reply["id"]!.GetValue<long>(), ["body"] = body, ["created"] = reply["created"]!.GetValue<string>(), ["is_developer_reply"] = true });
        }
    }

    private static HttpResponseMessage Json(JsonNode body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode status, string detail)
        => new(status) { Content = new StringContent(new JsonObject { ["detail"] = detail }.ToJsonString(), Encoding.UTF8, "application/json") };
}
