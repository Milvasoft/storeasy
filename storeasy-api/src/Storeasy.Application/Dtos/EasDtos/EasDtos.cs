using Storeasy.Domain.Eas;
using System.Linq.Expressions;

namespace Storeasy.Application.Dtos.EasDtos;

/// <summary>
/// Payload of an <see cref="StoreJobKind.EasSync"/> job.
/// </summary>
/// <param name="AppId">One app; null for every linked app of the organization.</param>
public sealed record EasSyncPayload(int? AppId = null);

/// <summary>
/// Result of a sync.
/// </summary>
/// <param name="Projects">Projects read.</param>
/// <param name="Builds">Builds read.</param>
/// <param name="Failed">Apps whose project could not be read.</param>
public sealed record EasSyncSummary(int Projects, int Builds, List<string> Failed);

/// <summary>
/// The organization's Expo connection: never the token.
/// </summary>
public class ExpoConnectionDto
{
    /// <summary>Id.</summary>
    public int Id { get; set; }

    /// <summary>The Expo actor (user or robot) the token belongs to.</summary>
    public string Fingerprint { get; set; }

    /// <summary>Result of the last check.</summary>
    public ConnectionStatus Status { get; set; }

    /// <summary>When Expo last accepted the token.</summary>
    public DateTime? LastVerifiedDate { get; set; }

    /// <summary>Why the last check failed.</summary>
    public string LastVerifyError { get; set; }

    /// <summary>Projection.</summary>
    public static Expression<Func<ExpoConnection, ExpoConnectionDto>> Projection { get; } = c => new ExpoConnectionDto
    {
        Id = c.Id,
        Fingerprint = c.Fingerprint,
        Status = c.Status,
        LastVerifiedDate = c.LastVerifiedDate,
        LastVerifyError = c.LastVerifyError,
    };
}

/// <summary>
/// An Expo project the token can see.
/// </summary>
public class EasProjectDto
{
    /// <summary>Project id.</summary>
    public string Id { get; set; }

    /// <summary>Account.</summary>
    public string AccountName { get; set; }

    /// <summary>Slug.</summary>
    public string Slug { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; }

    /// <summary>Linked GitHub repository, or null.</summary>
    public string GitHubRepository { get; set; }

    /// <summary>Bundle id or package name of its latest build.</summary>
    public string AppIdentifier { get; set; }

    /// <summary>How well it matches the asked app (3 identifier, 2 name, 0 none).</summary>
    public int Match { get; set; }

    /// <summary>App already linked to it in this organization.</summary>
    public int? LinkedAppId { get; set; }
}

/// <summary>
/// An app's EAS project link.
/// </summary>
public class EasProjectLinkDto
{
    /// <summary>Project id.</summary>
    public string ProjectId { get; set; }

    /// <summary>Account.</summary>
    public string AccountName { get; set; }

    /// <summary>Slug.</summary>
    public string Slug { get; set; }

    /// <summary>Linked GitHub repository, or null.</summary>
    public string GitHubRepository { get; set; }

    /// <summary>Its default branch.</summary>
    public string DefaultBranch { get; set; }

    /// <summary>The project on expo.dev.</summary>
    public string ProjectUrl { get; set; }

    /// <summary>Where the GitHub repository is connected on expo.dev.</summary>
    public string GitHubSettingsUrl { get; set; }

    /// <summary>When the builds were last read.</summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>Why the last read failed.</summary>
    public string SyncError { get; set; }

    /// <summary>What happens after a build finished, unless a build chooses otherwise.</summary>
    public EasChainDto Chain { get; set; } = new();
}

/// <summary>
/// The EAS state of an app.
/// </summary>
public class EasAppDto
{
    /// <summary>App.</summary>
    public int AppId { get; set; }

    /// <summary>App name.</summary>
    public string AppName { get; set; }

    /// <summary>Whether the organization has an Expo connection.</summary>
    public bool Connected { get; set; }

    /// <summary>Link, or null.</summary>
    public EasProjectLinkDto Link { get; set; }

    /// <summary>Recent builds, newest first.</summary>
    public List<EasBuildDto> Builds { get; set; } = [];

    /// <summary>Recent EAS Submit submissions, newest first.</summary>
    public List<EasSubmissionDto> Submissions { get; set; } = [];
}

/// <summary>
/// The build profiles offered when starting a build.
/// </summary>
public class EasProfilesDto
{
    /// <summary>Profile names.</summary>
    public List<string> Profiles { get; set; } = [];

    /// <summary>Whether they come from eas.json in the repository (else the defaults).</summary>
    public bool FromRepository { get; set; }

    /// <summary>Why eas.json could not be read, when it could not.</summary>
    public string Note { get; set; }
}

/// <summary>
/// An EAS build.
/// </summary>
public class EasBuildDto
{
    /// <summary>storeasy id.</summary>
    public long Id { get; set; }

    /// <summary>App.</summary>
    public int AppId { get; set; }

    /// <summary>Expo build id.</summary>
    public string BuildId { get; set; }

    /// <summary>Platform.</summary>
    public EasPlatform Platform { get; set; }

    /// <summary>State.</summary>
    public EasBuildStatus Status { get; set; }

    /// <summary>Build profile.</summary>
    public string Profile { get; set; }

    /// <summary>Git ref asked for.</summary>
    public string GitRef { get; set; }

    /// <summary>Commit.</summary>
    public string GitCommitHash { get; set; }

    /// <summary>App version.</summary>
    public string AppVersion { get; set; }

    /// <summary>Build number.</summary>
    public string AppBuildVersion { get; set; }

    /// <summary>Distribution.</summary>
    public string Distribution { get; set; }

    /// <summary>Who started it.</summary>
    public string StartedBy { get; set; }

    /// <summary>Whether storeasy started it.</summary>
    public bool StartedInStoreasy { get; set; }

    /// <summary>Page on expo.dev.</summary>
    public string PageUrl { get; set; }

    /// <summary>Logs.</summary>
    public string LogsUrl { get; set; }

    /// <summary>Package.</summary>
    public string ArtifactUrl { get; set; }

    /// <summary>Started.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>Ended.</summary>
    public DateTime? FinishedDate { get; set; }

    /// <summary>Error code.</summary>
    public string ErrorCode { get; set; }

    /// <summary>Error message.</summary>
    public string ErrorMessage { get; set; }

    /// <summary>Intake session it was sent to.</summary>
    public long? ArtifactSessionId { get; set; }

    /// <summary>What happens once it finished.</summary>
    public EasAfterBuild AfterBuild { get; set; }

    /// <summary>Whether the review or production plan is prepared once it reached its test channel.</summary>
    public bool PrepareReviewPlan { get; set; }

    /// <summary>Play track of the chained EAS Submit.</summary>
    public string SubmitTrack { get; set; }

    /// <summary>The prepared plans, pending until a member approves them.</summary>
    public List<long> ReviewPlanIds { get; set; } = [];

    /// <summary>Why a chained step could not run.</summary>
    public string ChainError { get; set; }

    /// <summary>Projection.</summary>
    public static Expression<Func<EasBuild, EasBuildDto>> Projection { get; } = b => new EasBuildDto
    {
        Id = b.Id,
        AppId = b.AppId,
        BuildId = b.BuildId,
        Platform = b.Platform,
        Status = b.Status,
        Profile = b.Profile,
        GitRef = b.GitRef,
        GitCommitHash = b.GitCommitHash,
        AppVersion = b.AppVersion,
        AppBuildVersion = b.AppBuildVersion,
        Distribution = b.Distribution,
        StartedBy = b.StartedBy,
        StartedInStoreasy = b.StartedInStoreasy,
        PageUrl = b.PageUrl,
        LogsUrl = b.LogsUrl,
        ArtifactUrl = b.ArtifactUrl,
        StartedDate = b.StartedDate,
        FinishedDate = b.FinishedDate,
        ErrorCode = b.ErrorCode,
        ErrorMessage = b.ErrorMessage,
        ArtifactSessionId = b.ArtifactSessionId,
        AfterBuild = b.AfterBuild,
        PrepareReviewPlan = b.PrepareReviewPlan,
        SubmitTrack = b.SubmitTrack,
        ReviewPlanIds = b.ReviewPlanIds,
        ChainError = b.ChainError,
    };

    private static readonly Lazy<Func<EasBuild, EasBuildDto>> _compiled = new(() => Projection.Compile());

    /// <summary>The DTO of a loaded build.</summary>
    public static EasBuildDto From(EasBuild build) => _compiled.Value(build);
}

/// <summary>
/// What happens after a build finished: the app's defaults, or the choice of one build.
/// </summary>
public class EasChainDto
{
    /// <summary>Nothing, send to intake, or EAS Submit.</summary>
    public EasAfterBuild AfterBuild { get; set; } = EasAfterBuild.None;

    /// <summary>Once the build reached its test channel, prepare (never approve) the review or production plan.</summary>
    public bool PrepareReviewPlan { get; set; }

    /// <summary>Play track of EAS Submit (Android): internal, alpha, beta or production.</summary>
    public string SubmitTrack { get; set; }

    /// <summary>Play release status of EAS Submit (Android): COMPLETED, DRAFT, IN_PROGRESS or HALTED.</summary>
    public string SubmitReleaseStatus { get; set; }
}

/// <summary>
/// An EAS Submit submission.
/// </summary>
public class EasSubmissionDto
{
    /// <summary>storeasy id.</summary>
    public long Id { get; set; }

    /// <summary>App.</summary>
    public int AppId { get; set; }

    /// <summary>storeasy id of the build.</summary>
    public long EasBuildId { get; set; }

    /// <summary>Expo submission id.</summary>
    public string SubmissionId { get; set; }

    /// <summary>Platform.</summary>
    public EasPlatform Platform { get; set; }

    /// <summary>State.</summary>
    public EasSubmissionStatus Status { get; set; }

    /// <summary>Play track.</summary>
    public string Track { get; set; }

    /// <summary>Play release status.</summary>
    public string ReleaseStatus { get; set; }

    /// <summary>Who started it.</summary>
    public string StartedBy { get; set; }

    /// <summary>Whether the build's chain option started it.</summary>
    public bool FromChain { get; set; }

    /// <summary>Started.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>Ended.</summary>
    public DateTime? FinishedDate { get; set; }

    /// <summary>Logs.</summary>
    public string LogsUrl { get; set; }

    /// <summary>Error code.</summary>
    public string ErrorCode { get; set; }

    /// <summary>Error message.</summary>
    public string ErrorMessage { get; set; }

    /// <summary>Projection.</summary>
    public static Expression<Func<EasSubmission, EasSubmissionDto>> Projection { get; } = s => new EasSubmissionDto
    {
        Id = s.Id,
        AppId = s.AppId,
        EasBuildId = s.EasBuildId,
        SubmissionId = s.SubmissionId,
        Platform = s.Platform,
        Status = s.Status,
        Track = s.Track,
        ReleaseStatus = s.ReleaseStatus,
        StartedBy = s.StartedBy,
        FromChain = s.FromChain,
        StartedDate = s.StartedDate,
        FinishedDate = s.FinishedDate,
        LogsUrl = s.LogsUrl,
        ErrorCode = s.ErrorCode,
        ErrorMessage = s.ErrorMessage,
    };

    private static readonly Lazy<Func<EasSubmission, EasSubmissionDto>> _compiled = new(() => Projection.Compile());

    /// <summary>The DTO of a loaded submission.</summary>
    public static EasSubmissionDto From(EasSubmission submission) => _compiled.Value(submission);
}
