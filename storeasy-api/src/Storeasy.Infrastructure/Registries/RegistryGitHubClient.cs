using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="IRegistryGitHub"/> over the GitHub REST API (named HTTP client <see cref="HttpClientName"/>, API version
/// 2022-11-28) with a fine-grained personal access token. Contents and the git data API commit version bumps and create
/// tags; the Actions API dispatches workflows and reads runs. Tests replace the handler with a fake GitHub.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed class RegistryGitHubClient(IHttpClientFactory httpClientFactory, RegistryOptions options) : IRegistryGitHub
{
    /// <summary>
    /// Name of the HTTP client.
    /// </summary>
    public const string HttpClientName = "registries-github";

    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly RegistryOptions _options = options;

    /// <inheritdoc/>
    public async Task<string> GetLoginAsync(string token, CancellationToken cancellationToken)
        => Text((await SendAsync(token, HttpMethod.Get, "/user", null, cancellationToken))?["login"]) ?? throw new StoreCallException("GitHub did not name the token's user.", null, transient: false);

    /// <inheritdoc/>
    public async Task<GitHubFile> GetFileAsync(string token, string repository, string path, string reference, CancellationToken cancellationToken)
    {
        var node = await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/contents/{EscapePath(path)}{Ref(reference)}", null, cancellationToken);

        if (node == null)
            return null;

        var content = Text(node["content"]) ?? string.Empty;
        var text = Text(node["encoding"]) == "base64" ? Encoding.UTF8.GetString(Convert.FromBase64String(content.Replace("\n", string.Empty, StringComparison.Ordinal))) : content;

        return new GitHubFile(path, Text(node["sha"]), text);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ListWorkflowFilesAsync(string token, string repository, string reference, CancellationToken cancellationToken)
    {
        var node = await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/contents/.github/workflows{Ref(reference)}", null, cancellationToken);

        return [.. (node as JsonArray ?? []).Where(f => Text(f?["type"]) == "file").Select(f => Text(f["name"])).Where(n => n != null && (n.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))];
    }

    /// <inheritdoc/>
    public async Task<string> GetBranchShaAsync(string token, string repository, string branch, CancellationToken cancellationToken)
        => Text((await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/git/ref/heads/{EscapePath(branch)}", null, cancellationToken))?["object"]?["sha"])
           ?? throw new StoreCallException($"The branch {branch} was not found in {repository}.", 404, transient: false);

    /// <inheritdoc/>
    public async Task<bool> TagExistsAsync(string token, string repository, string tag, CancellationToken cancellationToken)
        => await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/git/ref/tags/{EscapePath(tag)}", null, cancellationToken) != null;

    /// <inheritdoc/>
    public async Task<string> CommitFilesAsync(string token, string repository, string branch, IReadOnlyList<(string Path, string Sha, string Content)> files, string message, CancellationToken cancellationToken)
    {
        if (files.Count == 1)
        {
            var (path, sha, content) = files[0];
            var body = new JsonObject { ["message"] = message, ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)), ["sha"] = sha, ["branch"] = branch };

            return Text((await SendAsync(token, HttpMethod.Put, $"/repos/{repository}/contents/{EscapePath(path)}", body, cancellationToken))?["commit"]?["sha"])
                   ?? throw new StoreCallException("GitHub did not name the commit.", null, transient: false);
        }

        // Several files in one commit: blobs, a tree on the branch head's tree, a commit and a fast forward of the branch.
        var head = await GetBranchShaAsync(token, repository, branch, cancellationToken);
        var baseTree = Text((await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/git/commits/{head}", null, cancellationToken))?["tree"]?["sha"]);
        var entries = new JsonArray();

        foreach (var (path, _, content) in files)
        {
            var blob = await SendAsync(token, HttpMethod.Post, $"/repos/{repository}/git/blobs", new JsonObject { ["content"] = content, ["encoding"] = "utf-8" }, cancellationToken);

            entries.Add(new JsonObject { ["path"] = path, ["mode"] = "100644", ["type"] = "blob", ["sha"] = Text(blob?["sha"]) });
        }

        var tree = await SendAsync(token, HttpMethod.Post, $"/repos/{repository}/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, cancellationToken);
        var commit = await SendAsync(token, HttpMethod.Post, $"/repos/{repository}/git/commits", new JsonObject { ["message"] = message, ["tree"] = Text(tree?["sha"]), ["parents"] = new JsonArray(head) }, cancellationToken);
        var commitSha = Text(commit?["sha"]) ?? throw new StoreCallException("GitHub did not name the commit.", null, transient: false);

        await SendAsync(token, HttpMethod.Patch, $"/repos/{repository}/git/refs/heads/{EscapePath(branch)}", new JsonObject { ["sha"] = commitSha, ["force"] = false }, cancellationToken);

        return commitSha;
    }

    /// <inheritdoc/>
    public async Task CreateTagAsync(string token, string repository, string tag, string sha, CancellationToken cancellationToken)
        => await SendAsync(token, HttpMethod.Post, $"/repos/{repository}/git/refs", new JsonObject { ["ref"] = $"refs/tags/{tag}", ["sha"] = sha }, cancellationToken);

    /// <inheritdoc/>
    public async Task<GitHubRun> DispatchAsync(string token, string repository, string workflow, string branch, IReadOnlyDictionary<string, string> inputs, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["ref"] = branch, ["inputs"] = new JsonObject([.. inputs.Select(i => KeyValuePair.Create(i.Key, (JsonNode)i.Value))]) };
        var answer = await SendAsync(token, HttpMethod.Post, $"/repos/{repository}/actions/workflows/{Uri.EscapeDataString(workflow)}/dispatches", body, cancellationToken);

        // GitHub answers the run (workflow_run_id, html_url) where it names it; older answers have no body.
        return answer?["workflow_run_id"] is JsonValue id && id.TryGetValue<long>(out var runId)
            ? new GitHubRun(runId, $".github/workflows/{workflow}", "workflow_dispatch", "queued", null, null, Text(answer["html_url"]))
            : null;
    }

    /// <inheritdoc/>
    public async Task<List<GitHubRun>> ListRunsAsync(string token, string repository, string headSha, CancellationToken cancellationToken)
    {
        var node = await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/actions/runs?head_sha={Uri.EscapeDataString(headSha)}&per_page=50", null, cancellationToken);

        return [.. (node?["workflow_runs"] as JsonArray ?? []).Select(Run).Where(r => r != null)];
    }

    /// <inheritdoc/>
    public async Task<GitHubRun> GetRunAsync(string token, string repository, long runId, CancellationToken cancellationToken)
        => Run(await SendAsync(token, HttpMethod.Get, $"/repos/{repository}/actions/runs/{runId.ToString(CultureInfo.InvariantCulture)}", null, cancellationToken));

    private static GitHubRun Run(JsonNode node)
        => node?["id"] is JsonValue id && id.TryGetValue<long>(out var runId)
            ? new GitHubRun(runId, Text(node["path"]), Text(node["event"]), Text(node["status"]), Text(node["conclusion"]), Text(node["head_sha"]), Text(node["html_url"]))
            : null;

    private async Task<JsonNode> SendAsync(string token, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, $"{_options.GitHubApiBaseUrl.TrimEnd('/')}{path}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        if (body != null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);

            if (response.StatusCode == HttpStatusCode.NotFound && method == HttpMethod.Get)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;

                throw new StoreCallException($"GitHub answered {status} to {method} {path.Split('?')[0]}: {Shorten(text)}", status, status == 429 || status >= 500);
            }

            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("GitHub did not answer in time.", null, transient: true);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException($"GitHub could not be reached: {Shorten(ex.Message)}", null, transient: true);
        }
        catch (JsonException)
        {
            throw new StoreCallException("GitHub answered something that is not JSON.", null, transient: false);
        }
    }

    /// <summary>
    /// The <c>ref</c> query of a contents read; none reads the default branch.
    /// </summary>
    private static string Ref(string reference) => string.IsNullOrEmpty(reference) ? string.Empty : $"?ref={Uri.EscapeDataString(reference)}";

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string Text(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static string Shorten(string text) => string.IsNullOrEmpty(text) || text.Length <= _maxErrorLength ? text : text[.._maxErrorLength];
}
