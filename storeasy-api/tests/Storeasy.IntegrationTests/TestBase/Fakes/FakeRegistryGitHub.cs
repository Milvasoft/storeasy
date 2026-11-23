using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// In-memory GitHub behind the releases' HTTP handler: repositories with branches and files (contents API and the git data
/// API: refs, commits, blobs, trees), tags, workflow files, workflow dispatches and Actions runs, in the shapes of the REST
/// API (version 2022-11-28). A token other than <see cref="Token"/> answers 401. Each test uses its own repository.
/// </summary>
public sealed class FakeRegistryGitHub : HttpMessageHandler
{
    /// <summary>GitHub API address the tests configure.</summary>
    public const string BaseUrl = "https://api.github.fake";

    /// <summary>The token GitHub accepts.</summary>
    public const string Token = "github_pat_fake_0123456789abcdefABCDEF";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (string Sha, string Content)> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _heads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _blobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(string Path, string Blob)>> _trees = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _commitTrees = new(StringComparer.Ordinal);
    private readonly Dictionary<long, JsonObject> _runs = [];
    private long _next = 1000;

    /// <summary>Tags by "repo|tag" with the commit they point at.</summary>
    public ConcurrentDictionary<string, string> Tags { get; } = new(StringComparer.Ordinal);

    /// <summary>Every call as "METHOD /path".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>Workflow dispatches as "repo|workflow|ref|inputs json".</summary>
    public ConcurrentQueue<string> Dispatches { get; } = new();

    /// <summary>
    /// A file on a branch (the branch is created with a first commit when missing). Returns the file's sha.
    /// </summary>
    public string SetFile(string repository, string branch, string path, string content)
    {
        lock (_lock)
        {
            var sha = NewSha("file");

            _files[$"{repository}|{branch}|{path}"] = (sha, content);
            _heads[$"{repository}|{branch}"] = NewSha("commit");

            return sha;
        }
    }

    /// <summary>A file's content on a branch, or null.</summary>
    public string GetFile(string repository, string branch, string path)
    {
        lock (_lock)
            return _files.TryGetValue($"{repository}|{branch}|{path}", out var file) ? file.Content : null;
    }

    /// <summary>The head commit of a branch.</summary>
    public string Head(string repository, string branch)
    {
        lock (_lock)
            return _heads.GetValueOrDefault($"{repository}|{branch}");
    }

    /// <summary>A workflow run on a commit (as a push or tag would start one). Returns its id.</summary>
    public long AddRun(string repository, string headSha, string workflow, string status = "in_progress", string conclusion = null, string @event = "push")
    {
        lock (_lock)
        {
            var id = ++_next;

            _runs[id] = Run(repository, id, headSha, workflow, status, conclusion, @event);

            return id;
        }
    }

    /// <summary>Changes the status of a run.</summary>
    public void SetRun(long id, string status, string conclusion)
    {
        lock (_lock)
        {
            _runs[id]["status"] = status;
            _runs[id]["conclusion"] = conclusion;
        }
    }

    /// <summary>The ids of the runs on a commit.</summary>
    public List<long> RunsOf(string headSha)
    {
        lock (_lock)
            return [.. _runs.Where(r => r.Value["head_sha"]?.GetValue<string>() == headSha).Select(r => r.Key)];
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var body = request.Content == null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));

        Calls.Enqueue($"{request.Method} {path}");

        if (request.Headers.Authorization?.Parameter != Token)
            return Json(HttpStatusCode.Unauthorized, new JsonObject { ["message"] = "Bad credentials" });

        if (path == "/user")
            return Json(HttpStatusCode.OK, new JsonObject { ["login"] = "octocat" });

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4 || parts[0] != "repos")
            return NotFound();

        var repository = $"{parts[1]}/{parts[2]}";
        var rest = string.Join('/', parts[3..]);

        lock (_lock)
            return Answer(request.Method, repository, rest, query, body);
    }

    private HttpResponseMessage Answer(HttpMethod method, string repository, string rest, System.Collections.Specialized.NameValueCollection query, JsonNode body)
    {
        if (rest.StartsWith("contents/", StringComparison.Ordinal))
            return Contents(method, repository, rest["contents/".Length..], query["ref"] ?? "master", body);

        if (method == HttpMethod.Get && rest.StartsWith("git/ref/heads/", StringComparison.Ordinal))
        {
            var head = _heads.GetValueOrDefault($"{repository}|{rest["git/ref/heads/".Length..]}");

            return head == null ? NotFound() : Json(HttpStatusCode.OK, new JsonObject { ["object"] = new JsonObject { ["sha"] = head, ["type"] = "commit" } });
        }

        if (method == HttpMethod.Get && rest.StartsWith("git/ref/tags/", StringComparison.Ordinal))
            return Tags.TryGetValue($"{repository}|{rest["git/ref/tags/".Length..]}", out var tagged) ? Json(HttpStatusCode.OK, new JsonObject { ["object"] = new JsonObject { ["sha"] = tagged } }) : NotFound();

        if (method == HttpMethod.Post && rest == "git/refs")
        {
            var name = body!["ref"]!.GetValue<string>();

            if (!name.StartsWith("refs/tags/", StringComparison.Ordinal) || !Tags.TryAdd($"{repository}|{name["refs/tags/".Length..]}", body["sha"]!.GetValue<string>()))
                return Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["message"] = "Reference already exists" });

            return Json(HttpStatusCode.Created, new JsonObject { ["ref"] = name });
        }

        if (method == HttpMethod.Get && rest.StartsWith("git/commits/", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, new JsonObject { ["tree"] = new JsonObject { ["sha"] = _commitTrees.GetValueOrDefault(rest["git/commits/".Length..], "base-tree") } });

        if (method == HttpMethod.Post && rest == "git/blobs")
        {
            var sha = NewSha("blob");

            _blobs[sha] = body!["content"]!.GetValue<string>();

            return Json(HttpStatusCode.Created, new JsonObject { ["sha"] = sha });
        }

        if (method == HttpMethod.Post && rest == "git/trees")
        {
            var sha = NewSha("tree");

            _trees[sha] = [.. body!["tree"]!.AsArray().Select(e => (e!["path"]!.GetValue<string>(), e["sha"]!.GetValue<string>()))];

            return Json(HttpStatusCode.Created, new JsonObject { ["sha"] = sha });
        }

        if (method == HttpMethod.Post && rest == "git/commits")
        {
            var sha = NewSha("commit");

            _commitTrees[sha] = body!["tree"]!.GetValue<string>();

            return Json(HttpStatusCode.Created, new JsonObject { ["sha"] = sha });
        }

        if (method == HttpMethod.Patch && rest.StartsWith("git/refs/heads/", StringComparison.Ordinal))
            return MoveBranch(repository, rest["git/refs/heads/".Length..], body);

        if (method == HttpMethod.Post && rest.StartsWith("actions/workflows/", StringComparison.Ordinal) && rest.EndsWith("/dispatches", StringComparison.Ordinal))
        {
            var workflow = rest["actions/workflows/".Length..^"/dispatches".Length];
            var branch = body!["ref"]!.GetValue<string>();
            var id = ++_next;

            Dispatches.Enqueue($"{repository}|{workflow}|{branch}|{body["inputs"]?.ToJsonString()}");
            _runs[id] = Run(repository, id, _heads.GetValueOrDefault($"{repository}|{branch}"), workflow, "queued", null, "workflow_dispatch");

            return Json(HttpStatusCode.OK, new JsonObject { ["workflow_run_id"] = id, ["run_url"] = $"{BaseUrl}/repos/{repository}/actions/runs/{id}", ["html_url"] = $"https://github.com/{repository}/actions/runs/{id}" });
        }

        if (method == HttpMethod.Get && rest == "actions/runs")
        {
            var sha = query["head_sha"];

            return Json(HttpStatusCode.OK, new JsonObject { ["workflow_runs"] = new JsonArray([.. _runs.Values.Where(r => r["head_sha"]?.GetValue<string>() == sha).Select(r => r.DeepClone())]) });
        }

        if (method == HttpMethod.Get && rest.StartsWith("actions/runs/", StringComparison.Ordinal) && long.TryParse(rest["actions/runs/".Length..], out var runId))
            return _runs.TryGetValue(runId, out var run) ? Json(HttpStatusCode.OK, run.DeepClone()) : NotFound();

        return NotFound();
    }

    private HttpResponseMessage Contents(HttpMethod method, string repository, string path, string branch, JsonNode body)
    {
        if (method == HttpMethod.Get && path == ".github/workflows")
        {
            var prefix = $"{repository}|{branch}|.github/workflows/";
            var files = _files.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => k[prefix.Length..]).Where(n => !n.Contains('/', StringComparison.Ordinal)).ToList();

            return files.Count == 0 ? NotFound() : Json(HttpStatusCode.OK, new JsonArray([.. files.Select(n => (JsonNode)new JsonObject { ["name"] = n, ["type"] = "file", ["path"] = $".github/workflows/{n}" })]));
        }

        if (method == HttpMethod.Get)
        {
            return _files.TryGetValue($"{repository}|{branch}|{path}", out var file)
                ? Json(HttpStatusCode.OK, new JsonObject { ["path"] = path, ["sha"] = file.Sha, ["encoding"] = "base64", ["content"] = Wrap(Convert.ToBase64String(Encoding.UTF8.GetBytes(file.Content))) })
                : NotFound();
        }

        if (method != HttpMethod.Put)
            return NotFound();

        var target = body!["branch"]!.GetValue<string>();
        var key = $"{repository}|{target}|{path}";

        if (!_files.TryGetValue(key, out var current) || current.Sha != body["sha"]?.GetValue<string>())
            return Json(HttpStatusCode.Conflict, new JsonObject { ["message"] = $"{path} does not match {body["sha"]}" });

        var commit = NewSha("commit");

        _files[key] = (NewSha("file"), Encoding.UTF8.GetString(Convert.FromBase64String(body["content"]!.GetValue<string>())));
        _heads[$"{repository}|{target}"] = commit;

        return Json(HttpStatusCode.OK, new JsonObject { ["commit"] = new JsonObject { ["sha"] = commit, ["message"] = body["message"]?.GetValue<string>() } });
    }

    private HttpResponseMessage MoveBranch(string repository, string branch, JsonNode body)
    {
        var commit = body!["sha"]!.GetValue<string>();

        if (!_heads.ContainsKey($"{repository}|{branch}") || !_commitTrees.TryGetValue(commit, out var tree))
            return Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["message"] = "Update is not a fast forward" });

        foreach (var (path, blob) in _trees[tree])
            _files[$"{repository}|{branch}|{path}"] = (NewSha("file"), _blobs[blob]);

        _heads[$"{repository}|{branch}"] = commit;

        return Json(HttpStatusCode.OK, new JsonObject { ["object"] = new JsonObject { ["sha"] = commit } });
    }

    private static JsonObject Run(string repository, long id, string headSha, string workflow, string status, string conclusion, string @event) => new()
    {
        ["id"] = id,
        ["path"] = workflow.StartsWith(".github/", StringComparison.Ordinal) ? workflow : $".github/workflows/{workflow}",
        ["event"] = @event,
        ["status"] = status,
        ["conclusion"] = conclusion,
        ["head_sha"] = headSha,
        ["html_url"] = $"https://github.com/{repository}/actions/runs/{id}",
    };

    private string NewSha(string kind) => $"{kind}{++_next:x}".PadRight(40, '0');

    // GitHub wraps base64 content at 60 characters.
    private static string Wrap(string base64) => string.Join('\n', base64.Chunk(60).Select(c => new string(c)));

    private static HttpResponseMessage NotFound() => Json(HttpStatusCode.NotFound, new JsonObject { ["message"] = "Not Found" });

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body) => new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
