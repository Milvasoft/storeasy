namespace Storeasy.Application.Interfaces;

/// <summary>
/// The Expo actor an access token belongs to.
/// </summary>
/// <param name="Id">Actor id.</param>
/// <param name="Kind">GraphQL type: <c>User</c>, <c>SSOUser</c> or <c>Robot</c>.</param>
/// <param name="DisplayName">Name shown by Expo.</param>
/// <param name="Accounts">Accounts the actor belongs to.</param>
public sealed record ExpoActor(string Id, string Kind, string DisplayName, List<string> Accounts);

/// <summary>
/// An Expo project (app).
/// </summary>
/// <param name="Id">Project id.</param>
/// <param name="AccountName">Owning account.</param>
/// <param name="Slug">Slug.</param>
/// <param name="Name">Name.</param>
/// <param name="GitHubRepository">Linked GitHub repository (owner/repo), or null.</param>
/// <param name="DefaultBranch">Its default branch.</param>
/// <param name="AppIdentifier">Bundle id or package name of the latest build, when one exists.</param>
public sealed record ExpoProject(string Id, string AccountName, string Slug, string Name, string GitHubRepository, string DefaultBranch, string AppIdentifier);

/// <summary>
/// An EAS build as Expo reports it.
/// </summary>
/// <param name="Id">Build id.</param>
/// <param name="Platform">Platform.</param>
/// <param name="Status">State.</param>
/// <param name="Profile">Build profile.</param>
/// <param name="AppVersion">App version.</param>
/// <param name="AppBuildVersion">Build number or version code.</param>
/// <param name="GitCommitHash">Commit.</param>
/// <param name="Distribution">Distribution.</param>
/// <param name="Actor">Who started it.</param>
/// <param name="LogsUrl">First log file.</param>
/// <param name="ArtifactUrl">Built package.</param>
/// <param name="CreatedDate">Created.</param>
/// <param name="CompletedDate">Ended.</param>
/// <param name="ErrorCode">Error code.</param>
/// <param name="ErrorMessage">Error message.</param>
/// <param name="AccountName">Project's account.</param>
/// <param name="Slug">Project's slug.</param>
public sealed record ExpoBuild(string Id,
                               EasPlatform Platform,
                               EasBuildStatus Status,
                               string Profile,
                               string AppVersion,
                               string AppBuildVersion,
                               string GitCommitHash,
                               string Distribution,
                               string Actor,
                               string LogsUrl,
                               string ArtifactUrl,
                               DateTime? CreatedDate,
                               DateTime? CompletedDate,
                               string ErrorCode,
                               string ErrorMessage,
                               string AccountName,
                               string Slug);

/// <summary>
/// An Expo background job receipt (a build requested from GitHub).
/// </summary>
/// <param name="Id">Receipt id.</param>
/// <param name="State">QUEUED, IN_PROGRESS, SUCCESS or FAILURE.</param>
/// <param name="ResultId">The created build's id once the state is SUCCESS.</param>
/// <param name="ErrorMessage">Why it failed.</param>
public sealed record ExpoReceipt(string Id, string State, string ResultId, string ErrorMessage);

/// <summary>
/// An EAS Submit submission as Expo reports it.
/// </summary>
/// <param name="Id">Submission id.</param>
/// <param name="Platform">Platform.</param>
/// <param name="Status">State.</param>
/// <param name="LogsUrl">First log file.</param>
/// <param name="CreatedDate">Created.</param>
/// <param name="CompletedDate">Ended.</param>
/// <param name="ErrorCode">Error code.</param>
/// <param name="ErrorMessage">Error message.</param>
public sealed record ExpoSubmission(string Id, EasPlatform Platform, EasSubmissionStatus Status, string LogsUrl, DateTime? CreatedDate, DateTime? CompletedDate, string ErrorCode, string ErrorMessage);

/// <summary>
/// The submit credentials stored on expo.dev for an app identifier.
/// </summary>
/// <param name="AscApiKeyId">App Store Connect API key for submissions (iOS).</param>
/// <param name="GoogleServiceAccountKeyId">Google service account key for submissions (Android).</param>
public sealed record ExpoSubmitCredentials(string AscApiKeyId, string GoogleServiceAccountKeyId);

/// <summary>
/// What an EAS Submit sends.
/// </summary>
/// <param name="ProjectId">Expo project.</param>
/// <param name="BuildId">Expo build.</param>
/// <param name="Platform">Platform.</param>
/// <param name="AscAppIdentifier">Apple app id (iOS).</param>
/// <param name="AscApiKeyId">Expo's id of the App Store Connect API key (iOS).</param>
/// <param name="ApplicationIdentifier">Package name (Android).</param>
/// <param name="GoogleServiceAccountKeyId">Expo's id of the Google service account key (Android).</param>
/// <param name="Track">Play track (Android).</param>
/// <param name="ReleaseStatus">Play release status (Android).</param>
public sealed record ExpoSubmitRequest(string ProjectId, string BuildId, EasPlatform Platform, string AscAppIdentifier, string AscApiKeyId, string ApplicationIdentifier, string GoogleServiceAccountKeyId, string Track, string ReleaseStatus);

/// <summary>
/// The Expo GraphQL API (<c>https://api.expo.dev/graphql</c>) with an access token as <c>Authorization: Bearer</c>, the
/// operations eas-cli and expo.dev use. Failures throw <c>StoreCallException</c>.
/// </summary>
public interface IExpoApi
{
    /// <summary><c>meActor</c>: whose token it is and their accounts.</summary>
    Task<ExpoActor> GetActorAsync(string token, CancellationToken cancellationToken);

    /// <summary>The projects of every account of the actor (<c>meActor.accounts.apps</c>).</summary>
    Task<List<ExpoProject>> ListProjectsAsync(string token, CancellationToken cancellationToken);

    /// <summary><c>app.byId</c>; null when Expo has no such project for the token.</summary>
    Task<ExpoProject> GetProjectAsync(string token, string projectId, CancellationToken cancellationToken);

    /// <summary>The recent builds of a project (<c>app.byId.builds</c>), newest first.</summary>
    Task<List<ExpoBuild>> ListBuildsAsync(string token, string projectId, int limit, CancellationToken cancellationToken);

    /// <summary><c>builds.byId</c>.</summary>
    Task<ExpoBuild> GetBuildAsync(string token, string buildId, CancellationToken cancellationToken);

    /// <summary><c>githubApp.createGitHubBuild</c>: asks Expo to build from the linked GitHub repository at a git ref.</summary>
    Task<ExpoReceipt> StartGitHubBuildAsync(string token, string projectId, EasPlatform platform, string profile, string gitRef, CancellationToken cancellationToken);

    /// <summary><c>backgroundJobReceipt.byId</c>.</summary>
    Task<ExpoReceipt> GetReceiptAsync(string token, string receiptId, CancellationToken cancellationToken);

    /// <summary><c>build.cancelBuild</c>; returns the new state.</summary>
    Task<EasBuildStatus> CancelBuildAsync(string token, string buildId, CancellationToken cancellationToken);

    /// <summary>The submit credentials on expo.dev (<c>app.byId.iosAppCredentials</c> or <c>androidAppCredentials</c>) for an app identifier.</summary>
    Task<ExpoSubmitCredentials> GetSubmitCredentialsAsync(string token, string projectId, EasPlatform platform, string appIdentifier, CancellationToken cancellationToken);

    /// <summary><c>submission.createIosSubmission</c> or <c>createAndroidSubmission</c> of a finished build.</summary>
    Task<ExpoSubmission> StartSubmissionAsync(string token, ExpoSubmitRequest request, CancellationToken cancellationToken);

    /// <summary><c>submissions.byId</c>.</summary>
    Task<ExpoSubmission> GetSubmissionAsync(string token, string submissionId, CancellationToken cancellationToken);
}
