using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// In-memory Expo GraphQL API behind the EAS HTTP handler. It answers the operations storeasy sends (by operation name, in
/// the shapes of Expo's schema): the actor of a token, projects with their GitHub repository, builds, GitHub build requests
/// (background job receipts) and cancels. An unknown token answers a GraphQL error like Expo does. Each test uses its own
/// tokens and project ids.
/// </summary>
public sealed class FakeExpo : HttpMessageHandler
{
    /// <summary>GraphQL address the tests configure.</summary>
    public const string ApiUrl = "https://api.expo.fake/graphql";

    private readonly Lock _lock = new();
    private long _next = 5000;

    /// <summary>Actor display names by token.</summary>
    public ConcurrentDictionary<string, string> Tokens { get; } = new(StringComparer.Ordinal);

    /// <summary>Projects by token.</summary>
    public ConcurrentDictionary<string, List<JsonObject>> Projects { get; } = new(StringComparer.Ordinal);

    /// <summary>Builds by id.</summary>
    public ConcurrentDictionary<string, JsonObject> Builds { get; } = new(StringComparer.Ordinal);

    /// <summary>Receipts by id.</summary>
    public ConcurrentDictionary<string, JsonObject> Receipts { get; } = new(StringComparer.Ordinal);

    /// <summary>GitHub build requests as "projectId|platform|profile|gitRef".</summary>
    public ConcurrentQueue<string> GitHubBuilds { get; } = new();

    /// <summary>Canceled build ids.</summary>
    public ConcurrentQueue<string> Cancels { get; } = new();

    /// <summary>Submissions by id.</summary>
    public ConcurrentDictionary<string, JsonObject> Submissions { get; } = new(StringComparer.Ordinal);

    /// <summary>Submission requests as "platform|buildId|config json".</summary>
    public ConcurrentQueue<string> SubmissionRequests { get; } = new();

    /// <summary>Projects with submit credentials on expo.dev (an App Store Connect API key and a Google service account key).</summary>
    public ConcurrentDictionary<string, bool> SubmitCredentials { get; } = new(StringComparer.Ordinal);

    /// <summary>Operation names called.</summary>
    public ConcurrentQueue<string> Operations { get; } = new();

    /// <summary>Whether a GitHub build request creates the build at once (else the receipt stays queued).</summary>
    public bool CreateAtOnce { get; set; }

    /// <summary>A token and its actor; returns the token.</summary>
    public string AddToken(string actor)
    {
        var token = $"expo_{Guid.NewGuid():N}";

        Tokens[token] = actor;

        return token;
    }

    /// <summary>A project the token can see.</summary>
    public string AddProject(string token, string account, string slug, string repository = null, string appIdentifier = null)
    {
        var id = Guid.NewGuid().ToString();
        var project = new JsonObject
        {
            ["id"] = id,
            ["name"] = slug,
            ["slug"] = slug,
            ["ownerAccount"] = new JsonObject { ["name"] = account },
            ["githubRepository"] = repository == null ? null : new JsonObject
            {
                ["metadata"] = new JsonObject { ["githubRepoOwnerName"] = repository.Split('/')[0], ["githubRepoName"] = repository.Split('/')[1], ["defaultBranch"] = "main" },
            },
            ["latestBuilds"] = new JsonArray(new JsonObject { ["appIdentifier"] = appIdentifier }),
        };

        Projects.AddOrUpdate(token, _ => [project], (_, list) => [.. list, project]);

        return id;
    }

    /// <summary>A build of a project (as eas-cli would have started it); returns its id.</summary>
    public string AddBuild(string projectId, string platform, string status, string profile = "production", string buildUrl = null)
    {
        lock (_lock)
        {
            var project = Projects.Values.SelectMany(p => p).First(p => p["id"]!.GetValue<string>() == projectId);
            var id = Guid.NewGuid().ToString();

            Builds[id] = new JsonObject
            {
                ["id"] = id,
                ["projectId"] = projectId,
                ["status"] = status,
                ["platform"] = platform,
                ["buildProfile"] = profile,
                ["appVersion"] = "1.4.0",
                ["appBuildVersion"] = (++_next).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["gitCommitHash"] = "abc123def",
                ["distribution"] = "STORE",
                ["createdAt"] = DateTime.UtcNow.AddMinutes(-5).ToString("O"),
                ["completedAt"] = null,
                ["logFileUrls"] = new JsonArray($"https://logs.expo.fake/{id}.txt"),
                ["error"] = null,
                ["artifacts"] = buildUrl == null ? null : new JsonObject { ["buildUrl"] = buildUrl },
                ["initiatingActor"] = new JsonObject { ["displayName"] = "cli-user" },
                ["app"] = new JsonObject { ["slug"] = project["slug"]!.DeepClone(), ["ownerAccount"] = project["ownerAccount"]!.DeepClone() },
            };

            return id;
        }
    }

    /// <summary>Moves a build on (finished with a package, errored...).</summary>
    public void SetBuild(string id, string status, string buildUrl = null, string error = null)
    {
        lock (_lock)
        {
            var build = Builds[id];

            build["status"] = status;
            build["completedAt"] = status is "FINISHED" or "ERRORED" or "CANCELED" ? DateTime.UtcNow.ToString("O") : null;
            build["artifacts"] = buildUrl == null ? null : new JsonObject { ["buildUrl"] = buildUrl };
            build["error"] = error == null ? null : new JsonObject { ["errorCode"] = "EAS_BUILD_FAILED", ["message"] = error };
        }
    }

    /// <summary>Moves a submission on.</summary>
    public void SetSubmission(string id, string status, string error = null)
    {
        lock (_lock)
        {
            Submissions[id]["status"] = status;
            Submissions[id]["completedAt"] = status is "FINISHED" or "ERRORED" or "CANCELED" ? DateTime.UtcNow.ToString("O") : null;
            Submissions[id]["error"] = error == null ? null : new JsonObject { ["errorCode"] = "SUBMISSION_FAILED", ["message"] = error };
        }
    }

    /// <summary>Lets queued receipts create their builds.</summary>
    public void ResolveReceipts()
    {
        lock (_lock)
        {
            foreach (var receipt in Receipts.Values.Where(r => r["state"]!.GetValue<string>() == "QUEUED"))
                Resolve(receipt);
        }
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
        var query = body["query"]!.GetValue<string>();
        var variables = body["variables"] as JsonObject ?? [];
        var token = request.Headers.Authorization?.Parameter ?? string.Empty;
        var operation = query.Split(' ', '(', '{')[1];

        Operations.Enqueue(operation);

        if (!Tokens.TryGetValue(token, out var actor))
            return Json(new JsonObject { ["data"] = null, ["errors"] = new JsonArray(new JsonObject { ["message"] = "Unauthorized", ["extensions"] = new JsonObject { ["errorCode"] = "UNAUTHORIZED_ERROR" } }) });

        lock (_lock)
            return Json(new JsonObject { ["data"] = Answer(operation, variables, token, actor) });
    }

    private JsonNode Answer(string operation, JsonObject variables, string token, string actor)
    {
        var projects = Projects.GetValueOrDefault(token) ?? [];

        switch (operation)
        {
            case "StoreasyViewer":
                return new JsonObject { ["meActor"] = new JsonObject { ["__typename"] = "Robot", ["id"] = "actor-1", ["displayName"] = actor, ["accounts"] = new JsonArray(new JsonObject { ["id"] = "acc", ["name"] = "kuzey" }) } };
            case "StoreasyProjects":
                return new JsonObject { ["meActor"] = new JsonObject { ["accounts"] = new JsonArray(new JsonObject { ["name"] = "kuzey", ["apps"] = new JsonArray([.. projects.Select(p => p.DeepClone())]) }) } };
            case "StoreasyProject":
                return new JsonObject { ["app"] = new JsonObject { ["byId"] = projects.FirstOrDefault(p => p["id"]!.GetValue<string>() == variables["appId"]!.GetValue<string>())?.DeepClone() } };
            case "StoreasyBuilds":
                var projectId = variables["appId"]!.GetValue<string>();

                return new JsonObject
                {
                    ["app"] = new JsonObject
                    {
                        ["byId"] = new JsonObject { ["id"] = projectId, ["builds"] = new JsonArray([.. Builds.Values.Where(b => b["projectId"]!.GetValue<string>() == projectId).OrderByDescending(b => b["createdAt"]!.GetValue<string>()).Select(Public)]) },
                    },
                };
            case "StoreasyBuild":
                return new JsonObject { ["builds"] = new JsonObject { ["byId"] = Builds.TryGetValue(variables["buildId"]!.GetValue<string>(), out var build) ? Public(build) : null } };
            case "StoreasyGitHubBuild":
                return new JsonObject { ["githubApp"] = new JsonObject { ["createGitHubBuild"] = RequestBuild(variables["buildInput"]!.AsObject(), projects) } };
            case "StoreasyReceipt":
                return new JsonObject { ["backgroundJobReceipt"] = new JsonObject { ["byId"] = Receipts.GetValueOrDefault(variables["id"]!.GetValue<string>())?.DeepClone() } };
            case "StoreasyCancelBuild":
                var id = variables["buildId"]!.GetValue<string>();

                Cancels.Enqueue(id);
                Builds[id]["status"] = "CANCELED";

                return new JsonObject { ["build"] = new JsonObject { ["cancelBuild"] = new JsonObject { ["id"] = id, ["status"] = "CANCELED" } } };
            case "StoreasyIosSubmitCredentials":
                return new JsonObject { ["app"] = new JsonObject { ["byId"] = new JsonObject { ["id"] = variables["appId"]!.DeepClone(), ["iosAppCredentials"] = SubmitCredentials.ContainsKey(variables["appId"]!.GetValue<string>()) ? new JsonArray(new JsonObject { ["appleAppIdentifier"] = new JsonObject { ["bundleIdentifier"] = "any" }, ["appStoreConnectApiKeyForSubmissions"] = new JsonObject { ["id"] = "asc-key-1" } }) : new JsonArray() } } };
            case "StoreasyAndroidSubmitCredentials":
                return new JsonObject { ["app"] = new JsonObject { ["byId"] = new JsonObject { ["id"] = variables["appId"]!.DeepClone(), ["androidAppCredentials"] = SubmitCredentials.ContainsKey(variables["appId"]!.GetValue<string>()) ? new JsonArray(new JsonObject { ["applicationIdentifier"] = "any", ["googleServiceAccountKeyForSubmissions"] = new JsonObject { ["id"] = "gsa-key-1" } }) : new JsonArray() } } };
            case "StoreasySubmit":
                var input = variables["input"]!.AsObject();
                var field = input["config"]!["track"] == null ? "createIosSubmission" : "createAndroidSubmission";
                var submission = new JsonObject
                {
                    ["id"] = Guid.NewGuid().ToString(),
                    ["status"] = "IN_QUEUE",
                    ["platform"] = field == "createIosSubmission" ? "IOS" : "ANDROID",
                    ["createdAt"] = DateTime.UtcNow.ToString("O"),
                    ["completedAt"] = null,
                    ["logFiles"] = new JsonArray("https://logs.expo.fake/submission.txt"),
                    ["error"] = null,
                };

                SubmissionRequests.Enqueue($"{submission["platform"]}|{input["submittedBuildId"]}|{input["config"]!.ToJsonString()}");
                Submissions[submission["id"]!.GetValue<string>()] = submission;

                return new JsonObject { ["submission"] = new JsonObject { [field] = new JsonObject { ["submission"] = submission.DeepClone() } } };
            case "StoreasySubmission":
                return new JsonObject { ["submissions"] = new JsonObject { ["byId"] = Submissions.GetValueOrDefault(variables["submissionId"]!.GetValue<string>())?.DeepClone() } };
            default:
                return null;
        }
    }

    private JsonObject RequestBuild(JsonObject input, List<JsonObject> projects)
    {
        var projectId = input["appId"]!.GetValue<string>();
        var platform = input["platform"]!.GetValue<string>();

        GitHubBuilds.Enqueue($"{projectId}|{platform}|{input["buildProfile"]}|{input["gitRef"]}");

        var receipt = new JsonObject { ["id"] = $"receipt-{++_next}", ["state"] = "QUEUED", ["resultId"] = null, ["errorMessage"] = null, ["platform"] = platform, ["projectId"] = projectId, ["profile"] = input["buildProfile"]!.DeepClone() };
        var project = projects.FirstOrDefault(p => p["id"]!.GetValue<string>() == projectId);

        if (project?["githubRepository"] == null)
        {
            receipt["state"] = "FAILURE";
            receipt["errorMessage"] = "The app is not linked to a GitHub repository.";
        }
        else if (CreateAtOnce)
        {
            Resolve(receipt);
        }

        Receipts[receipt["id"]!.GetValue<string>()] = receipt;

        return (JsonObject)receipt.DeepClone();
    }

    private void Resolve(JsonObject receipt)
    {
        var id = AddBuild(receipt["projectId"]!.GetValue<string>(), receipt["platform"]!.GetValue<string>(), "IN_QUEUE", receipt["profile"]!.GetValue<string>());

        Builds[id]["initiatingActor"] = new JsonObject { ["displayName"] = "storeasy-robot" };
        receipt["state"] = "SUCCESS";
        receipt["resultId"] = id;
    }

    private static JsonNode Public(JsonObject build)
    {
        var clone = build.DeepClone().AsObject();

        clone.Remove("projectId");

        return clone;
    }

    private static HttpResponseMessage Json(JsonNode body) => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}
