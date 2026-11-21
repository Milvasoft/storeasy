using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Winget;
using Storeasy.Infrastructure.Stores.Microsoft;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// <see cref="IWingetClient"/> over the GitHub REST API (named HTTP client <see cref="HttpClientName"/>) and installer
/// downloads (<see cref="DownloadClientName"/>, redirects followed by hand so every hop is checked against private
/// addresses; the GitHub token never goes to an installer host). Endpoints: <c>GET /user</c>, <c>GET /repos/{fork}</c>,
/// contents of winget-pkgs, <c>GET /search/issues</c>, pulls, check runs and issue comments; the write is
/// <c>POST /repos/{fork}/merge-upstream</c>, git trees, commits and refs on the fork, then <c>POST /repos/{upstream}/pulls</c>.
/// Tests replace both handlers with a fake GitHub.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed partial class WingetClient(IHttpClientFactory httpClientFactory, WingetOptions options) : IWingetClient
{
    /// <summary>
    /// Name of the HTTP client of GitHub calls.
    /// </summary>
    public const string HttpClientName = "winget";

    /// <summary>
    /// Name of the HTTP client of installer downloads.
    /// </summary>
    public const string DownloadClientName = "winget-downloads";

    /// <summary>
    /// Most redirects followed for one installer download.
    /// </summary>
    public const int MaxRedirects = 5;

    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly WingetOptions _options = options;

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"\[!\[[^\]]*\]\([^)]*\)\]\([^)]*\)|!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex BadgeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();

    #region Verify

    /// <inheritdoc/>
    public async Task<StoreVerification> VerifyAsync(string secretJson, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);

        using (var user = await SendRawAsync(credentials.Token, HttpMethod.Get, "/user", null, cancellationToken))
        {
            if (!user.IsSuccessStatusCode)
                return await RefusalAsync(user, "GitHub refused the token", cancellationToken);

            // Classic tokens list their scopes; writing to a public fork needs public_repo (or repo).
            if (user.Headers.TryGetValues("X-OAuth-Scopes", out var scopeHeaders))
            {
                var scopes = string.Join(',', scopeHeaders).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (!scopes.Contains("public_repo") && !scopes.Contains("repo"))
                    return new StoreVerification(ConnectionStatus.Rejected, "The classic token needs the public_repo scope to push to the fork and open pull requests.");
            }
        }

        using var fork = await SendRawAsync(credentials.Token, HttpMethod.Get, $"/repos/{credentials.Fork}", null, cancellationToken);

        if (fork.StatusCode == HttpStatusCode.NotFound)
            return new StoreVerification(ConnectionStatus.Rejected, $"The fork {credentials.Fork} was not found or the token cannot see it.");

        if (!fork.IsSuccessStatusCode)
            return await RefusalAsync(fork, "GitHub refused to read the fork", cancellationToken);

        var repository = JsonNode.Parse(await fork.Content.ReadAsStringAsync(cancellationToken));
        var parent = Text(repository?["parent"], "full_name") ?? Text(repository?["source"], "full_name");

        if (repository?["fork"]?.GetValue<bool>() != true || !string.Equals(parent, _options.Upstream, StringComparison.OrdinalIgnoreCase))
            return new StoreVerification(ConnectionStatus.Rejected, $"{credentials.Fork} is not a fork of {_options.Upstream}.");

        if (repository["permissions"]?["push"]?.GetValue<bool>() != true)
            return new StoreVerification(ConnectionStatus.Rejected, $"The token cannot push to {credentials.Fork}.");

        return new StoreVerification(ConnectionStatus.Verified, null);
    }

    #endregion

    #region Reads

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListVersionsAsync(string secretJson, string packageIdentifier, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);
        var items = await GetJsonOrNullAsync(credentials.Token, ContentsPath(WingetPaths.PackageFolder(packageIdentifier)), cancellationToken);

        if (items is not JsonArray list)
            return [];

        // A package folder also holds the folders of longer identifiers (Publisher.App.Beta in Publisher/App/Beta); version
        // folders start with a digit (or "v" and a digit).
        return WingetVersions.Descending(list.Where(i => Text(i, "type") == "dir").Select(i => Text(i, "name")).Where(IsVersionFolder));
    }

    /// <summary>
    /// Whether a folder name of a package folder is a version (starts with a digit, or <c>v</c> and a digit).
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool IsVersionFolder(string name)
        => !string.IsNullOrEmpty(name) && (char.IsAsciiDigit(name[0]) || (name.Length > 1 && name[0] is 'v' or 'V' && char.IsAsciiDigit(name[1])));

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> ReadManifestFilesAsync(string secretJson, string packageIdentifier, string version, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);
        var folder = WingetPaths.VersionFolder(packageIdentifier, version);
        var items = await GetJsonOrNullAsync(credentials.Token, ContentsPath(folder), cancellationToken);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        if (items is not JsonArray list)
            return files;

        foreach (var name in list.Where(i => Text(i, "type") == "file").Select(i => Text(i, "name")).Where(n => n != null && n.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
        {
            using var response = await SendAsync(credentials.Token, HttpMethod.Get, ContentsPath($"{folder}/{name}"), null, cancellationToken, "application/vnd.github.raw+json");

            files[name] = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        return files;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WingetPullRequestRead>> FindPullRequestsAsync(string secretJson, string packageIdentifier, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);
        var login = await LoginAsync(credentials.Token, cancellationToken);
        var query = $"repo:{_options.Upstream} is:pr author:{login} \"{packageIdentifier}\" in:title";
        var root = await GetJsonAsync(credentials.Token, $"/search/issues?q={Uri.EscapeDataString(query)}&sort=created&order=desc&per_page={Math.Clamp(_options.SearchLimit, 1, 100)}", cancellationToken);
        var pattern = new Regex($@"(^|[\s:]){Regex.Escape(packageIdentifier)}($|[\s,])", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

        return [.. (root["items"] as JsonArray ?? []).Where(i => i?["pull_request"] != null && pattern.IsMatch(Text(i, "title") ?? string.Empty)).Select(i => new WingetPullRequestRead
        {
            Number = i["number"]?.GetValue<int>() ?? 0,
            Url = Text(i, "html_url"),
            Title = Text(i, "title"),
            Open = Text(i, "state") == "open",
            Merged = Text(i["pull_request"], "merged_at") != null,
            Draft = i["draft"]?.GetValue<bool>() == true,
            Labels = Labels(i),
            CreatedDate = Date(i, "created_at"),
            MergedDate = Date(i["pull_request"], "merged_at"),
            ClosedDate = Date(i, "closed_at"),
        })];
    }

    /// <inheritdoc/>
    public async Task<WingetPullRequestRead> GetPullRequestAsync(string secretJson, int number, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);
        var pull = await GetJsonAsync(credentials.Token, $"/repos/{_options.Upstream}/pulls/{number}", cancellationToken);
        var read = new WingetPullRequestRead
        {
            Number = number,
            Url = Text(pull, "html_url"),
            Title = Text(pull, "title"),
            Open = Text(pull, "state") == "open",
            Merged = pull["merged"]?.GetValue<bool>() == true || Text(pull, "merged_at") != null,
            Draft = pull["draft"]?.GetValue<bool>() == true,
            HeadBranch = Text(pull["head"], "ref"),
            HeadSha = Text(pull["head"], "sha"),
            Labels = Labels(pull),
            CreatedDate = Date(pull, "created_at"),
            MergedDate = Date(pull, "merged_at"),
            ClosedDate = Date(pull, "closed_at"),
        };

        if (read.HeadSha != null)
        {
            // The validation pipeline reports as check runs; a token that may not read them leaves the list empty.
            var checks = await GetJsonOrNullAsync(credentials.Token, $"/repos/{_options.Upstream}/commits/{read.HeadSha}/check-runs?per_page=50", cancellationToken, tolerateForbidden: true);

            read.Checks = [.. (checks?["check_runs"] as JsonArray ?? []).Select(c => new WingetCheck
            {
                Name = Text(c, "name"),
                Status = Text(c, "status"),
                Conclusion = Text(c, "conclusion"),
                Url = Text(c, "html_url"),
            })];
        }

        var comments = await GetJsonOrNullAsync(credentials.Token, $"/repos/{_options.Upstream}/issues/{number}/comments?per_page=100", cancellationToken, tolerateForbidden: true);

        read.Comments = [.. (comments as JsonArray ?? []).Where(c => IsReviewer(c))
                                                          .Select(Comment)
                                                          .Where(c => c.Excerpt.Length > 0)
                                                          .OrderByDescending(c => c.Date)
                                                          .Take(WingetPullRequest.MaxComments)];

        return read;
    }

    private static bool IsReviewer(JsonNode comment)
    {
        var user = comment?["user"];
        var login = Text(user, "login") ?? string.Empty;

        return Text(user, "type") == "Bot"
               || login.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase)
               || login.Equals("wingetbot", StringComparison.OrdinalIgnoreCase)
               || Text(comment, "author_association") is "MEMBER" or "COLLABORATOR" or "OWNER";
    }

    /// <summary>
    /// A comment shortened for storage: HTML comments and badges removed, white space collapsed, cut to
    /// <see cref="WingetComment.ExcerptMaxLength"/>.
    /// </summary>
    /// <param name="comment"></param>
    /// <returns></returns>
    public static WingetComment Comment(JsonNode comment)
    {
        var body = Text(comment, "body") ?? string.Empty;

        body = HtmlCommentRegex().Replace(body, " ");
        body = BadgeRegex().Replace(body, " ");
        body = WhiteSpaceRegex().Replace(body, " ").Trim();

        var user = comment?["user"];
        var login = Text(user, "login");

        return new WingetComment
        {
            Author = login,
            IsBot = Text(user, "type") == "Bot" || login?.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase) == true || login?.Equals("wingetbot", StringComparison.OrdinalIgnoreCase) == true,
            Excerpt = SecretRedactor.RedactAndTrim(body, WingetComment.ExcerptMaxLength),
            Url = Text(comment, "html_url"),
            Date = Date(comment, "created_at") ?? DateTime.MinValue,
        };
    }

    #endregion

    #region Installer hash

    /// <inheritdoc/>
    public async Task<WingetInstallerHash> HashInstallerAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new StoreCallException("The installer address is not an http(s) address.", null, false);

        var client = _httpClientFactory.CreateClient(DownloadClientName);

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (_options.BlockPrivateDownloadAddresses)
                await DownloadGuard.EnsurePublicAsync(uri, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            request.Headers.UserAgent.ParseAdd("storeasy");

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new StoreCallException(SecretRedactor.RedactAndTrim($"Installer download failed: {ex.Message}", _maxErrorLength), null, true);
            }

            using (response)
            {
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
                {
                    uri = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(uri, response.Headers.Location);

                    if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                        throw new StoreCallException("The installer download redirects to a non http(s) address.", null, false);

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new StoreCallException($"Installer download answered {(int)response.StatusCode}.", (int)response.StatusCode, (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests);

                if (response.Content.Headers.ContentLength > _options.InstallerMaxBytes)
                    throw new StoreCallException($"The installer is larger than {_options.InstallerMaxBytes} bytes.", null, false);

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                return await HashAsync(stream, _options.InstallerMaxBytes, cancellationToken);
            }
        }

        throw new StoreCallException($"The installer download redirects more than {MaxRedirects} times.", null, false);
    }

    /// <summary>
    /// SHA-256 (upper case hex) and size of a stream, refusing more than <paramref name="maxBytes"/>.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="maxBytes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<WingetInstallerHash> HashAsync(Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;

            if (total > maxBytes)
                throw new StoreCallException($"The installer is larger than {maxBytes} bytes.", null, false);

            hash.AppendData(buffer, 0, read);
        }

        return new WingetInstallerHash(Convert.ToHexString(hash.GetHashAndReset()), total);
    }

    #endregion

    #region Pull request

    /// <inheritdoc/>
    public async Task<WingetOpenedPullRequest> OpenPullRequestAsync(string secretJson, WingetPullRequestDraft draft, CancellationToken cancellationToken)
    {
        var credentials = Credentials(secretJson);
        var fork = await GetJsonAsync(credentials.Token, $"/repos/{credentials.Fork}", cancellationToken);
        var forkBranch = Text(fork, "default_branch") ?? _options.UpstreamBranch;

        // Bring the fork up to date; a fork whose default branch diverged cannot be synced and the upstream head is used.
        using (await SendRawAsync(credentials.Token, HttpMethod.Post, $"/repos/{credentials.Fork}/merge-upstream", new JsonObject { ["branch"] = forkBranch }, cancellationToken))
        {
        }

        var head = await GetJsonAsync(credentials.Token, $"/repos/{_options.Upstream}/git/ref/heads/{_options.UpstreamBranch}", cancellationToken);
        var baseSha = Text(head["object"], "sha") ?? throw new StoreCallException("winget-pkgs has no head commit.", null, true);
        var baseCommit = await GetJsonAsync(credentials.Token, $"/repos/{_options.Upstream}/git/commits/{baseSha}", cancellationToken);
        var baseTree = Text(baseCommit["tree"], "sha") ?? throw new StoreCallException("The head commit of winget-pkgs has no tree.", null, true);

        var entries = new JsonArray();

        foreach (var file in draft.Files)
            entries.Add(new JsonObject { ["path"] = file.Path, ["mode"] = "100644", ["type"] = "blob", ["content"] = file.Content });

        foreach (var path in draft.DeletePaths)
            entries.Add(new JsonObject { ["path"] = path, ["mode"] = "100644", ["type"] = "blob", ["sha"] = null });

        var tree = await SendJsonAsync(credentials.Token, HttpMethod.Post, $"/repos/{credentials.Fork}/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, cancellationToken);
        var commit = await SendJsonAsync(credentials.Token, HttpMethod.Post, $"/repos/{credentials.Fork}/git/commits", new JsonObject
        {
            ["message"] = draft.CommitMessage,
            ["tree"] = Text(tree, "sha"),
            ["parents"] = new JsonArray(baseSha),
        }, cancellationToken);
        var commitSha = Text(commit, "sha") ?? throw new StoreCallException("GitHub did not return the commit.", null, true);

        using (var created = await SendRawAsync(credentials.Token, HttpMethod.Post, $"/repos/{credentials.Fork}/git/refs", new JsonObject { ["ref"] = $"refs/heads/{draft.Branch}", ["sha"] = commitSha }, cancellationToken))
        {
            if (created.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                // A retried job: the branch is storeasy's own, so it is moved to the new commit.
                using var moved = await SendAsync(credentials.Token, HttpMethod.Patch, $"/repos/{credentials.Fork}/git/refs/heads/{draft.Branch}", new JsonObject { ["sha"] = commitSha, ["force"] = true }, cancellationToken);
            }
            else if (!created.IsSuccessStatusCode)
            {
                throw await ErrorAsync(created, cancellationToken);
            }
        }

        var headLabel = $"{credentials.ForkOwner}:{draft.Branch}";

        using var pull = await SendRawAsync(credentials.Token, HttpMethod.Post, $"/repos/{_options.Upstream}/pulls", new JsonObject
        {
            ["title"] = draft.Title,
            ["head"] = headLabel,
            ["base"] = _options.UpstreamBranch,
            ["body"] = draft.Body,
            ["maintainer_can_modify"] = true,
        }, cancellationToken);

        var body = await pull.Content.ReadAsStringAsync(cancellationToken);

        if (pull.IsSuccessStatusCode)
        {
            var opened = JsonNode.Parse(body);

            return new WingetOpenedPullRequest(opened?["number"]?.GetValue<int>(), Text(opened, "html_url"), commitSha, null);
        }

        if (pull.StatusCode == HttpStatusCode.UnprocessableEntity && body.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await GetJsonAsync(credentials.Token, $"/repos/{_options.Upstream}/pulls?head={Uri.EscapeDataString(headLabel)}&state=open", cancellationToken);

            if (existing is JsonArray { Count: > 0 } list)
                return new WingetOpenedPullRequest(list[0]?["number"]?.GetValue<int>(), Text(list[0], "html_url"), commitSha, null);
        }

        if ((int)pull.StatusCode >= 500 || pull.StatusCode == HttpStatusCode.TooManyRequests)
            throw new StoreCallException(SecretRedactor.RedactAndTrim($"{(int)pull.StatusCode}: {StoreGateway.DescribeError(body)}", _maxErrorLength), (int)pull.StatusCode, true);

        // GitHub refused the pull request itself (a fine-grained token cannot open one in a repository its owner does not
        // own); the branch is pushed, so the owner can open it from the compare page.
        var compare = $"https://github.com/{_options.Upstream}/compare/{_options.UpstreamBranch}...{Uri.EscapeDataString(credentials.ForkOwner)}:{Uri.EscapeDataString(draft.Branch)}?expand=1";

        return new WingetOpenedPullRequest(null, compare, commitSha, SecretRedactor.RedactAndTrim($"{(int)pull.StatusCode}: {StoreGateway.DescribeError(body)}", _maxErrorLength));
    }

    #endregion

    #region HTTP

    private string ContentsPath(string path) => $"/repos/{_options.Upstream}/contents/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}?ref={Uri.EscapeDataString(_options.UpstreamBranch)}";

    private static WingetCredentials Credentials(string secretJson)
        => WingetCredentialParser.Parse(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

    private async Task<string> LoginAsync(string token, CancellationToken cancellationToken)
        => Text(await GetJsonAsync(token, "/user", cancellationToken), "login") ?? throw new StoreCallException("GitHub did not return the token's user.", null, false);

    private Task<JsonNode> GetJsonAsync(string token, string path, CancellationToken cancellationToken)
        => SendJsonAsync(token, HttpMethod.Get, path, null, cancellationToken);

    private async Task<JsonNode> GetJsonOrNullAsync(string token, string path, CancellationToken cancellationToken, bool tolerateForbidden = false)
    {
        using var response = await SendRawAsync(token, HttpMethod.Get, path, null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound || (tolerateForbidden && response.StatusCode == HttpStatusCode.Forbidden && !RateLimited(response)))
            return null;

        if (!response.IsSuccessStatusCode)
            throw await ErrorAsync(response, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private async Task<JsonNode> SendJsonAsync(string token, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(token, method, path, body, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text) ?? new JsonObject();
    }

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken, string accept = null)
    {
        var response = await SendRawAsync(token, method, path, body, cancellationToken, accept);

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            throw await ErrorAsync(response, cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(string token, HttpMethod method, string path, JsonNode body, CancellationToken cancellationToken, string accept = null)
    {
        using var request = new HttpRequestMessage(method, _options.ApiBaseUrl.TrimEnd('/') + path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd(accept ?? "application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("storeasy");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        if (body != null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            return await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException(SecretRedactor.RedactAndTrim($"GitHub unreachable: {ex.Message}", _maxErrorLength), null, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("GitHub did not answer in time.", null, true);
        }
    }

    private static bool RateLimited(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.FirstOrDefault() == "0";

    private static async Task<StoreCallException> ErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var transient = status >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests || (response.StatusCode == HttpStatusCode.Forbidden && RateLimited(response));

        return new StoreCallException(SecretRedactor.RedactAndTrim($"{status}: {StoreGateway.DescribeError(body)}", _maxErrorLength), status, transient);
    }

    private static async Task<StoreVerification> RefusalAsync(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        var error = await ErrorAsync(response, cancellationToken);
        var status = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound && !error.Transient
            ? ConnectionStatus.Rejected
            : ConnectionStatus.Unreachable;

        return new StoreVerification(status, $"{what}: {error.Message}");
    }

    private static List<string> Labels(JsonNode node) => [.. (node?["labels"] as JsonArray ?? []).Select(l => Text(l, "name")).Where(l => l != null)];

    private static string Text(JsonNode node, string name)
        => node is JsonObject obj && obj.TryGetPropertyValue(name, out var value) && value is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static DateTime? Date(JsonNode node, string name)
        => DateTimeOffset.TryParse(Text(node, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.UtcDateTime : null;

    #endregion
}
