using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Eas;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Eas;

/// <summary>
/// <see cref="IExpoApi"/> over Expo's GraphQL API (named HTTP client <see cref="HttpClientName"/>) with an access token as
/// <c>Authorization: Bearer</c>, as eas-cli sends <c>EXPO_TOKEN</c>. The operations follow the schema eas-cli is generated
/// from (packages/eas-cli/src/graphql/generated.ts): <c>meActor</c>, <c>app.byId</c>, <c>builds.byId</c>,
/// <c>backgroundJobReceipt.byId</c>, <c>githubApp.createGitHubBuild</c> and <c>build.cancelBuild</c>. Tests replace the
/// handler with a fake Expo.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed class ExpoClient(IHttpClientFactory httpClientFactory, EasOptions options) : IExpoApi
{
    /// <summary>
    /// Name of the HTTP client.
    /// </summary>
    public const string HttpClientName = "expo";

    private const int _maxErrorLength = 300;

    private const string _buildFields = """
        fragment StoreasyBuild on Build {
          id
          status
          platform
          buildProfile
          appVersion
          appBuildVersion
          gitCommitHash
          distribution
          createdAt
          completedAt
          logFileUrls
          error { errorCode message }
          artifacts { buildUrl }
          initiatingActor { displayName }
          app { slug ownerAccount { name } }
        }
        """;

    private const string _projectFields = """
        fragment StoreasyProject on App {
          id
          name
          slug
          ownerAccount { name }
          githubRepository { metadata { githubRepoOwnerName githubRepoName defaultBranch } }
          latestBuilds: builds(offset: 0, limit: 1) { appIdentifier }
        }
        """;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly EasOptions _options = options;

    /// <inheritdoc/>
    public async Task<ExpoActor> GetActorAsync(string token, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "query StoreasyViewer { meActor { __typename id displayName accounts { id name } } }", null, cancellationToken);
        var actor = data?["meActor"] ?? throw new StoreCallException("Expo did not name the token's actor.", null, transient: false);

        return new ExpoActor(Text(actor["id"]), Text(actor["__typename"]), Text(actor["displayName"]), [.. Array(actor["accounts"]).Select(a => Text(a?["name"])).Where(n => n != null)]);
    }

    /// <inheritdoc/>
    public async Task<List<ExpoProject>> ListProjectsAsync(string token, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "query StoreasyProjects { meActor { accounts { name apps(offset: 0, limit: 100, includeUnpublished: true) { ...StoreasyProject } } } }\n" + _projectFields, null, cancellationToken);

        return [.. Array(data?["meActor"]?["accounts"]).SelectMany(a => Array(a?["apps"])).Select(Project).Where(p => p != null).DistinctBy(p => p.Id)];
    }

    /// <inheritdoc/>
    public async Task<ExpoProject> GetProjectAsync(string token, string projectId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "query StoreasyProject($appId: String!) { app { byId(appId: $appId) { ...StoreasyProject } } }\n" + _projectFields, new JsonObject { ["appId"] = projectId }, cancellationToken);

        return Project(data?["app"]?["byId"]);
    }

    /// <inheritdoc/>
    public async Task<List<ExpoBuild>> ListBuildsAsync(string token, string projectId, int limit, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token,
                                   "query StoreasyBuilds($appId: String!, $limit: Int!) { app { byId(appId: $appId) { id builds(offset: 0, limit: $limit) { ...StoreasyBuild } } } }\n" + _buildFields,
                                   new JsonObject { ["appId"] = projectId, ["limit"] = Math.Clamp(limit, 1, 50) },
                                   cancellationToken);

        return [.. Array(data?["app"]?["byId"]?["builds"]).Select(Build).Where(b => b != null)];
    }

    /// <inheritdoc/>
    public async Task<ExpoBuild> GetBuildAsync(string token, string buildId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "query StoreasyBuild($buildId: ID!) { builds { byId(buildId: $buildId) { ...StoreasyBuild } } }\n" + _buildFields, new JsonObject { ["buildId"] = buildId }, cancellationToken);

        return Build(data?["builds"]?["byId"]);
    }

    /// <inheritdoc/>
    public async Task<ExpoReceipt> StartGitHubBuildAsync(string token, string projectId, EasPlatform platform, string profile, string gitRef, CancellationToken cancellationToken)
    {
        var input = new JsonObject
        {
            ["appId"] = projectId,
            ["platform"] = EasRules.ExpoPlatform(platform),
            ["buildProfile"] = profile,
            ["gitRef"] = gitRef,
        };
        var data = await SendAsync(token,
                                   "mutation StoreasyGitHubBuild($buildInput: GitHubBuildInput!) { githubApp { createGitHubBuild(buildInput: $buildInput) { id state resultId errorMessage } } }",
                                   new JsonObject { ["buildInput"] = input },
                                   cancellationToken);

        return Receipt(data?["githubApp"]?["createGitHubBuild"]) ?? throw new StoreCallException("Expo did not return the build request.", null, transient: false);
    }

    /// <inheritdoc/>
    public async Task<ExpoReceipt> GetReceiptAsync(string token, string receiptId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "query StoreasyReceipt($id: ID!) { backgroundJobReceipt { byId(id: $id) { id state resultId errorMessage } } }", new JsonObject { ["id"] = receiptId }, cancellationToken);

        return Receipt(data?["backgroundJobReceipt"]?["byId"]);
    }

    /// <inheritdoc/>
    public async Task<EasBuildStatus> CancelBuildAsync(string token, string buildId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "mutation StoreasyCancelBuild($buildId: ID!) { build { cancelBuild(buildId: $buildId) { id status } } }", new JsonObject { ["buildId"] = buildId }, cancellationToken);

        return EasRules.StatusOf(Text(data?["build"]?["cancelBuild"]?["status"]));
    }

    private const string _submissionFields = """
        fragment StoreasySubmission on Submission {
          id
          status
          platform
          createdAt
          completedAt
          logFiles
          error { errorCode message }
        }
        """;

    /// <inheritdoc/>
    public async Task<ExpoSubmitCredentials> GetSubmitCredentialsAsync(string token, string projectId, EasPlatform platform, string appIdentifier, CancellationToken cancellationToken)
    {
        if (platform == EasPlatform.Ios)
        {
            var ios = await SendAsync(token,
                                      "query StoreasyIosSubmitCredentials($appId: String!) { app { byId(appId: $appId) { id iosAppCredentials { appleAppIdentifier { bundleIdentifier } appStoreConnectApiKeyForSubmissions { id } } } } }",
                                      new JsonObject { ["appId"] = projectId },
                                      cancellationToken);
            var credentials = Array(ios?["app"]?["byId"]?["iosAppCredentials"]).ToList();
            var match = credentials.FirstOrDefault(c => appIdentifier != null && Text(c?["appleAppIdentifier"]?["bundleIdentifier"]) == appIdentifier) ?? credentials.FirstOrDefault();

            return new ExpoSubmitCredentials(Text(match?["appStoreConnectApiKeyForSubmissions"]?["id"]), null);
        }

        var android = await SendAsync(token,
                                      "query StoreasyAndroidSubmitCredentials($appId: String!) { app { byId(appId: $appId) { id androidAppCredentials { applicationIdentifier googleServiceAccountKeyForSubmissions { id } } } } }",
                                      new JsonObject { ["appId"] = projectId },
                                      cancellationToken);
        var keys = Array(android?["app"]?["byId"]?["androidAppCredentials"]).ToList();
        var key = keys.FirstOrDefault(c => appIdentifier != null && Text(c?["applicationIdentifier"]) == appIdentifier) ?? keys.FirstOrDefault();

        return new ExpoSubmitCredentials(null, Text(key?["googleServiceAccountKeyForSubmissions"]?["id"]));
    }

    /// <inheritdoc/>
    public async Task<ExpoSubmission> StartSubmissionAsync(string token, ExpoSubmitRequest request, CancellationToken cancellationToken)
    {
        var ios = request.Platform == EasPlatform.Ios;
        var config = ios
            ? new JsonObject { ["ascAppIdentifier"] = request.AscAppIdentifier, ["ascApiKeyId"] = request.AscApiKeyId }
            : new JsonObject
            {
                ["applicationIdentifier"] = request.ApplicationIdentifier,
                ["googleServiceAccountKeyId"] = request.GoogleServiceAccountKeyId,
                ["track"] = request.Track,
                ["releaseStatus"] = request.ReleaseStatus,
            };
        var input = new JsonObject { ["appId"] = request.ProjectId, ["submittedBuildId"] = request.BuildId, ["config"] = config };
        var field = ios ? "createIosSubmission" : "createAndroidSubmission";
        var type = ios ? "CreateIosSubmissionInput" : "CreateAndroidSubmissionInput";
        var data = await SendAsync(token,
                                   $"mutation StoreasySubmit($input: {type}!) {{ submission {{ {field}(input: $input) {{ submission {{ ...StoreasySubmission }} }} }} }}\n" + _submissionFields,
                                   new JsonObject { ["input"] = input },
                                   cancellationToken);

        return Submission(data?["submission"]?[field]?["submission"]) ?? throw new StoreCallException("Expo did not return the submission.", null, transient: false);
    }

    /// <inheritdoc/>
    public async Task<ExpoSubmission> GetSubmissionAsync(string token, string submissionId, CancellationToken cancellationToken)
    {
        var data = await SendAsync(token, "query StoreasySubmission($submissionId: ID!) { submissions { byId(submissionId: $submissionId) { ...StoreasySubmission } } }\n" + _submissionFields, new JsonObject { ["submissionId"] = submissionId }, cancellationToken);

        return Submission(data?["submissions"]?["byId"]);
    }

    private static ExpoSubmission Submission(JsonNode node)
        => Text(node?["id"]) is string id
            ? new ExpoSubmission(id,
                                 EasRules.PlatformOf(Text(node["platform"])),
                                 EasRules.SubmissionStatusOf(Text(node["status"])),
                                 Text(Array(node["logFiles"]).FirstOrDefault()),
                                 Date(node["createdAt"]),
                                 Date(node["completedAt"]),
                                 Text(node["error"]?["errorCode"]),
                                 Text(node["error"]?["message"]))
            : null;

    private static ExpoProject Project(JsonNode node)
    {
        if (Text(node?["id"]) is not string id)
            return null;

        var metadata = node["githubRepository"]?["metadata"];
        var repository = Text(metadata?["githubRepoOwnerName"]) is string owner && Text(metadata["githubRepoName"]) is string name ? $"{owner}/{name}" : null;

        return new ExpoProject(id,
                               Text(node["ownerAccount"]?["name"]),
                               Text(node["slug"]),
                               Text(node["name"]),
                               repository,
                               Text(metadata?["defaultBranch"]),
                               Text(Array(node["latestBuilds"]).FirstOrDefault()?["appIdentifier"]));
    }

    private static ExpoBuild Build(JsonNode node)
    {
        if (Text(node?["id"]) is not string id)
            return null;

        return new ExpoBuild(id,
                             EasRules.PlatformOf(Text(node["platform"])),
                             EasRules.StatusOf(Text(node["status"])),
                             Text(node["buildProfile"]),
                             Text(node["appVersion"]),
                             Text(node["appBuildVersion"]),
                             Text(node["gitCommitHash"]),
                             Text(node["distribution"]),
                             Text(node["initiatingActor"]?["displayName"]),
                             Text(Array(node["logFileUrls"]).FirstOrDefault()),
                             Text(node["artifacts"]?["buildUrl"]),
                             Date(node["createdAt"]),
                             Date(node["completedAt"]),
                             Text(node["error"]?["errorCode"]),
                             Text(node["error"]?["message"]),
                             Text(node["app"]?["ownerAccount"]?["name"]),
                             Text(node["app"]?["slug"]));
    }

    private static ExpoReceipt Receipt(JsonNode node)
        => Text(node?["id"]) is string id ? new ExpoReceipt(id, Text(node["state"]), Text(node["resultId"]), Text(node["errorMessage"])) : null;

    private async Task<JsonNode> SendAsync(string token, string query, JsonObject variables, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        request.Content = new StringContent(new JsonObject { ["query"] = query, ["variables"] = variables ?? [] }.ToJsonString(), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, timeout.Token);
            var text = await response.Content.ReadAsStringAsync(timeout.Token);
            var status = (int)response.StatusCode;
            var body = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);

            if (Array(body?["errors"]).FirstOrDefault() is JsonNode error)
                throw new StoreCallException($"Expo answered: {Shorten(Text(error["message"]))}", status == 200 ? 400 : status, status == 429 || status >= 500);

            if (!response.IsSuccessStatusCode)
                throw new StoreCallException($"Expo answered {status}: {Shorten(text)}", status, status == 429 || status >= 500);

            return body?["data"];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("Expo did not answer in time.", null, transient: true);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException($"Expo could not be reached: {Shorten(ex.Message)}", null, transient: true);
        }
        catch (JsonException)
        {
            throw new StoreCallException("Expo answered something that is not JSON.", null, transient: false);
        }
    }

    private static IEnumerable<JsonNode> Array(JsonNode node) => node as JsonArray ?? [];

    private static DateTime? Date(JsonNode node)
        => Text(node) is string text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    private static string Text(JsonNode node) => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static string Shorten(string text) => string.IsNullOrEmpty(text) || text.Length <= _maxErrorLength ? text : text[.._maxErrorLength];
}
