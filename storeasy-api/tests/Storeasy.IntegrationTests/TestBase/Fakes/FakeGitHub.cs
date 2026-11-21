using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// An in-memory GitHub behind the winget client's HTTP handlers: users by token, the upstream winget-pkgs repository
/// (manifest files by path), forks, git trees, commits and refs on forks, pull requests with labels, check runs and
/// comments, issue search, and installer downloads (any other host). Nothing leaves the process. Each test uses its own
/// token, login and package identifier.
/// </summary>
public sealed class FakeGitHub : HttpMessageHandler
{
    /// <summary>API base address the tests configure.</summary>
    public const string ApiBaseUrl = "https://api.github.fake";

    /// <summary>Upstream repository.</summary>
    public const string Upstream = "microsoft/winget-pkgs";

    /// <summary>Head commit of the upstream default branch.</summary>
    public const string BaseSha = "basecommit0000000000000000000000000000000";

    private static int _number = 300000;
    private static int _sequence;

    private readonly object _gate = new();

    /// <summary>A user behind a token.</summary>
    /// <param name="Login">GitHub login.</param>
    /// <param name="Scopes">X-OAuth-Scopes header of a classic token; null sends none.</param>
    public sealed record User(string Login, string Scopes);

    /// <summary>A fork.</summary>
    public sealed class Fork
    {
        public string FullName { get; set; }

        public string Parent { get; set; } = Upstream;

        public bool Push { get; set; } = true;

        public ConcurrentDictionary<string, string> Refs { get; } = new();
    }

    /// <summary>A pull request of the upstream.</summary>
    public sealed class Pull
    {
        public int Number { get; set; }

        public string Title { get; set; }

        public string Author { get; set; }

        public string Head { get; set; }

        public string HeadSha { get; set; }

        public string Body { get; set; }

        public bool Open { get; set; } = true;

        public bool Merged { get; set; }

        public bool Draft { get; set; }

        public List<string> Labels { get; set; } = [];

        public List<JsonObject> Checks { get; set; } = [];

        public List<JsonObject> Comments { get; set; } = [];

        public Dictionary<string, string> Files { get; set; } = [];

        public DateTime Created { get; set; } = DateTime.UtcNow;
    }

    public ConcurrentDictionary<string, User> Users { get; } = new();

    public ConcurrentDictionary<string, Fork> Forks { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Upstream files by path.</summary>
    public ConcurrentDictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<int, Pull> Pulls { get; } = new();

    /// <summary>Commit sha to the files it adds (path to content; null content deletes).</summary>
    public ConcurrentDictionary<string, Dictionary<string, string>> Commits { get; } = new();

    private ConcurrentDictionary<string, Dictionary<string, string>> Trees { get; } = new();

    /// <summary>Installer downloads by absolute address.</summary>
    public ConcurrentDictionary<string, byte[]> Downloads { get; } = new();

    /// <summary>Redirects of installer downloads: address to target.</summary>
    public ConcurrentDictionary<string, string> Redirects { get; } = new();

    /// <summary>Tokens whose pull requests GitHub refuses (a fine-grained token on an upstream it does not own).</summary>
    public ConcurrentDictionary<string, bool> RefusePulls { get; } = new();

    /// <summary>Every request as "METHOD host/path?query" (authorization never recorded).</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>Hosts that received an Authorization header.</summary>
    public ConcurrentQueue<string> AuthorizedHosts { get; } = new();

    public static string NewToken() => "ghp_" + Guid.NewGuid().ToString("N") + "abcd";

    public static string NewLogin() => "user" + Interlocked.Increment(ref _sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Adds a user with a classic token (public_repo scope) and a fork of winget-pkgs it may push to.
    /// </summary>
    public (string Token, string Login, string Fork) AddAccount(string scopes = "public_repo, read:org")
    {
        var token = NewToken();
        var login = NewLogin();
        var fork = $"{login}/winget-pkgs";

        Users[token] = new User(login, scopes);
        Forks[fork] = new Fork { FullName = fork };

        return (token, login, fork);
    }

    /// <summary>
    /// Adds the manifests of one version to the upstream.
    /// </summary>
    public void AddVersion(string packageIdentifier, string version, IReadOnlyDictionary<string, string> files)
    {
        var folder = $"manifests/{char.ToLowerInvariant(packageIdentifier[0])}/{packageIdentifier.Replace('.', '/')}/{version}";

        foreach (var (name, content) in files)
            Files[$"{folder}/{name}"] = content;
    }

    /// <summary>
    /// Merges a pull request: its files land in the upstream.
    /// </summary>
    public void Merge(int number)
    {
        var pull = Pulls[number];

        foreach (var (path, content) in pull.Files)
        {
            if (content == null)
                Files.TryRemove(path, out _);
            else
                Files[path] = content;
        }

        pull.Open = false;
        pull.Merged = true;
        pull.Labels.Add("Publish-Pipeline-Succeeded");
    }

    /// <summary>
    /// A pull request opened outside storeasy (found by the sync's search).
    /// </summary>
    public Pull AddPull(string author, string title, params string[] labels)
    {
        var pull = new Pull { Number = Interlocked.Increment(ref _number), Author = author, Title = title, Head = "outside", HeadSha = Guid.NewGuid().ToString("N"), Labels = [.. labels] };

        Pulls[pull.Number] = pull;

        return pull;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;

        Calls.Enqueue($"{request.Method} {uri.Host}{uri.AbsolutePath}{uri.Query}");

        if (request.Headers.Authorization != null)
            AuthorizedHosts.Enqueue(uri.Host);

        if (!ApiBaseUrl.EndsWith(uri.Host, StringComparison.Ordinal))
            return Download(uri);

        var token = request.Headers.Authorization?.Parameter;

        if (token == null || !Users.TryGetValue(token, out var user))
            return Json(HttpStatusCode.Unauthorized, new JsonObject { ["message"] = "Bad credentials" });

        var body = request.Content == null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var query = HttpUtility.ParseQueryString(uri.Query);
        var raw = request.Headers.Accept.Any(a => a.MediaType == "application/vnd.github.raw+json");

        lock (_gate)
        {
            return Route(request.Method, path, query, body, raw, token, user);
        }
    }

    private HttpResponseMessage Route(HttpMethod method, string path, System.Collections.Specialized.NameValueCollection query, JsonNode body, bool raw, string token, User user)
    {
        if (method == HttpMethod.Get && path == "/user")
        {
            var response = Json(HttpStatusCode.OK, new JsonObject { ["login"] = user.Login });

            if (user.Scopes != null)
                response.Headers.Add("X-OAuth-Scopes", user.Scopes);

            return response;
        }

        if (method == HttpMethod.Get && path == "/search/issues")
            return Search(query["q"], user);

        var upstream = $"/repos/{Upstream}";

        if (path.StartsWith(upstream + "/contents/", StringComparison.Ordinal))
            return Contents(path[(upstream.Length + "/contents/".Length)..], raw);

        if (method == HttpMethod.Get && path == $"{upstream}/git/ref/heads/master")
            return Json(HttpStatusCode.OK, new JsonObject { ["object"] = new JsonObject { ["sha"] = BaseSha } });

        if (method == HttpMethod.Get && path.StartsWith($"{upstream}/git/commits/", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, new JsonObject { ["sha"] = path[(path.LastIndexOf('/') + 1)..], ["tree"] = new JsonObject { ["sha"] = "basetree" } });

        if (method == HttpMethod.Post && path == $"{upstream}/pulls")
            return OpenPull(body, token, user);

        if (method == HttpMethod.Get && path == $"{upstream}/pulls")
        {
            var head = query["head"];
            var matches = Pulls.Values.Where(p => p.Open && $"{p.Author}:{p.Head}" == head).Select(PullJson).ToArray();

            return Json(HttpStatusCode.OK, new JsonArray(matches));
        }

        if (method == HttpMethod.Get && path.StartsWith($"{upstream}/pulls/", StringComparison.Ordinal) && int.TryParse(path[(upstream.Length + "/pulls/".Length)..], out var number))
            return Pulls.TryGetValue(number, out var pull) ? Json(HttpStatusCode.OK, PullJson(pull)) : NotFound();

        if (method == HttpMethod.Get && path.StartsWith($"{upstream}/commits/", StringComparison.Ordinal) && path.EndsWith("/check-runs", StringComparison.Ordinal))
        {
            var sha = path.Split('/')[^2];
            var runs = Pulls.Values.Where(p => p.HeadSha == sha).SelectMany(p => p.Checks).Select(c => (JsonNode)c.DeepClone()).ToArray();

            return Json(HttpStatusCode.OK, new JsonObject { ["total_count"] = runs.Length, ["check_runs"] = new JsonArray(runs) });
        }

        if (method == HttpMethod.Get && path.StartsWith($"{upstream}/issues/", StringComparison.Ordinal) && path.EndsWith("/comments", StringComparison.Ordinal) && int.TryParse(path.Split('/')[^2], out var issue))
            return Json(HttpStatusCode.OK, new JsonArray([.. (Pulls.TryGetValue(issue, out var commented) ? commented.Comments : []).Select(c => (JsonNode)c.DeepClone())]));

        if (method == HttpMethod.Get && path == upstream)
            return Json(HttpStatusCode.OK, new JsonObject { ["full_name"] = Upstream, ["fork"] = false });

        // Fork routes.
        var forkName = Forks.Keys.FirstOrDefault(f => path.Equals($"/repos/{f}", StringComparison.OrdinalIgnoreCase) || path.StartsWith($"/repos/{f}/", StringComparison.OrdinalIgnoreCase));

        if (forkName == null)
            return NotFound();

        var fork = Forks[forkName];
        var rest = path[$"/repos/{forkName}".Length..];

        return (method.Method, rest) switch
        {
            ("GET", "") => Json(HttpStatusCode.OK, new JsonObject
            {
                ["full_name"] = fork.FullName,
                ["fork"] = true,
                ["default_branch"] = "master",
                ["parent"] = new JsonObject { ["full_name"] = fork.Parent },
                ["permissions"] = new JsonObject { ["push"] = fork.Push },
            }),
            ("POST", "/merge-upstream") => Json(HttpStatusCode.OK, new JsonObject { ["message"] = "Successfully fetched and fast-forwarded from upstream." }),
            ("POST", "/git/trees") => CreateTree(body),
            ("POST", "/git/commits") => CreateCommit(body),
            ("POST", "/git/refs") => CreateRef(fork, body),
            ("PATCH", _) when rest.StartsWith("/git/refs/heads/", StringComparison.Ordinal) => MoveRef(fork, rest["/git/refs/heads/".Length..], body),
            _ => NotFound(),
        };
    }

    private HttpResponseMessage Contents(string path, bool raw)
    {
        path = path.TrimEnd('/');

        if (Files.TryGetValue(path, out var content))
        {
            return raw
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "text/plain") }
                : Json(HttpStatusCode.OK, new JsonObject { ["name"] = path[(path.LastIndexOf('/') + 1)..], ["type"] = "file", ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) });
        }

        var prefix = path + "/";
        var children = Files.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                                 .Select(k => k[prefix.Length..])
                                 .Select(k => k.Contains('/') ? (Name: k[..k.IndexOf('/')], Type: "dir") : (Name: k, Type: "file"))
                                 .Distinct()
                                 .OrderBy(c => c.Name, StringComparer.Ordinal)
                                 .ToList();

        if (children.Count == 0)
            return NotFound();

        return Json(HttpStatusCode.OK, new JsonArray([.. children.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["type"] = c.Type, ["path"] = prefix + c.Name })]));
    }

    private HttpResponseMessage Search(string q, User user)
    {
        // q: repo:microsoft/winget-pkgs is:pr author:LOGIN "Package.Id" in:title
        var author = q?.Split(' ').FirstOrDefault(t => t.StartsWith("author:", StringComparison.Ordinal))?["author:".Length..];
        var quoted = q?.Split('"') is { Length: >= 3 } parts ? parts[1] : null;
        var items = Pulls.Values.Where(p => p.Author == author && quoted != null && p.Title.Contains(quoted, StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(p => p.Number)
                                .Select(p =>
                                {
                                    var item = PullJson(p);

                                    item["pull_request"] = new JsonObject { ["merged_at"] = p.Merged ? "2026-11-20T10:00:00Z" : null };

                                    return (JsonNode)item;
                                })
                                .ToArray();

        return Json(HttpStatusCode.OK, new JsonObject { ["total_count"] = items.Length, ["items"] = new JsonArray(items) });
    }

    private HttpResponseMessage CreateTree(JsonNode body)
    {
        var sha = "tree" + Guid.NewGuid().ToString("N");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in body?["tree"] as JsonArray ?? [])
            files[entry["path"].GetValue<string>()] = entry["content"]?.GetValue<string>();

        Trees[sha] = files;

        return Json(HttpStatusCode.Created, new JsonObject { ["sha"] = sha });
    }

    private HttpResponseMessage CreateCommit(JsonNode body)
    {
        var sha = "commit" + Guid.NewGuid().ToString("N");

        Commits[sha] = Trees.GetValueOrDefault(body?["tree"]?.GetValue<string>() ?? string.Empty) ?? [];

        return Json(HttpStatusCode.Created, new JsonObject { ["sha"] = sha });
    }

    private HttpResponseMessage CreateRef(Fork fork, JsonNode body)
    {
        var name = body?["ref"]?.GetValue<string>()?["refs/heads/".Length..];

        if (name == null || !fork.Refs.TryAdd(name, body["sha"].GetValue<string>()))
            return Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["message"] = "Reference already exists" });

        return Json(HttpStatusCode.Created, new JsonObject { ["ref"] = body["ref"]?.GetValue<string>() });
    }

    private HttpResponseMessage MoveRef(Fork fork, string branch, JsonNode body)
    {
        fork.Refs[branch] = body?["sha"]?.GetValue<string>();

        return Json(HttpStatusCode.OK, new JsonObject { ["ref"] = $"refs/heads/{branch}" });
    }

    private HttpResponseMessage OpenPull(JsonNode body, string token, User user)
    {
        if (RefusePulls.ContainsKey(token))
            return Json(HttpStatusCode.Forbidden, new JsonObject { ["message"] = "Resource not accessible by personal access token" });

        var head = body?["head"]?.GetValue<string>() ?? string.Empty;
        var owner = head.Split(':')[0];
        var branch = head[(head.IndexOf(':') + 1)..];
        var fork = Forks.Values.FirstOrDefault(f => f.FullName.StartsWith(owner + "/", StringComparison.OrdinalIgnoreCase));

        if (fork == null || !fork.Refs.TryGetValue(branch, out var sha))
            return Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["message"] = "Validation Failed", ["errors"] = new JsonArray(new JsonObject { ["message"] = "head invalid" }) });

        if (Pulls.Values.Any(p => p.Open && p.Head == branch && p.Author == user.Login))
            return Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["message"] = $"A pull request already exists for {head}." });

        var pull = new Pull
        {
            Number = Interlocked.Increment(ref _number),
            Title = body["title"]?.GetValue<string>(),
            Body = body["body"]?.GetValue<string>(),
            Author = user.Login,
            Head = branch,
            HeadSha = sha,
            Files = Commits.GetValueOrDefault(sha) ?? [],
        };

        Pulls[pull.Number] = pull;

        return Json(HttpStatusCode.Created, PullJson(pull));
    }

    private static JsonObject PullJson(Pull p) => new()
    {
        ["number"] = p.Number,
        ["html_url"] = $"https://github.com/{Upstream}/pull/{p.Number}",
        ["title"] = p.Title,
        ["state"] = p.Open ? "open" : "closed",
        ["merged"] = p.Merged,
        ["merged_at"] = p.Merged ? "2026-11-20T10:00:00Z" : null,
        ["closed_at"] = p.Open ? null : "2026-11-20T10:00:00Z",
        ["created_at"] = p.Created.ToString("O"),
        ["draft"] = p.Draft,
        ["user"] = new JsonObject { ["login"] = p.Author },
        ["head"] = new JsonObject { ["ref"] = p.Head, ["sha"] = p.HeadSha },
        ["labels"] = new JsonArray([.. p.Labels.Select(l => (JsonNode)new JsonObject { ["name"] = l })]),
    };

    private HttpResponseMessage Download(Uri uri)
    {
        var address = uri.GetLeftPart(UriPartial.Path);

        if (Redirects.TryGetValue(address, out var target))
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);

            redirect.Headers.Location = new Uri(target);

            return redirect;
        }

        return Downloads.TryGetValue(address, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : NotFound();
    }

    private static HttpResponseMessage NotFound() => Json(HttpStatusCode.NotFound, new JsonObject { ["message"] = "Not Found" });

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body)
        => new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
