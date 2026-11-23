using Storeasy.Domain.Registries;
using System.Linq.Expressions;

namespace Storeasy.Application.Dtos.RegistryDtos;

/// <summary>
/// Payload of <see cref="StoreJobKind.RegistryPlanApply"/>.
/// </summary>
/// <param name="PlanId">The approved plan.</param>
public sealed record RegistryPlanApplyPayload(long PlanId);

/// <summary>
/// A credential of the package registries module, without its secret.
/// </summary>
public class RegistryConnectionDto
{
    /// <summary>Credential.</summary>
    public int Id { get; set; }

    /// <summary>What it is for.</summary>
    public RegistryConnectionKind Kind { get; set; }

    /// <summary>The GitHub login, or the last characters of a key.</summary>
    public string Fingerprint { get; set; }

    /// <summary>Result of the last check.</summary>
    public ConnectionStatus Status { get; set; }

    /// <summary>UTC instant of the last successful check.</summary>
    public DateTime? LastVerifiedDate { get; set; }

    /// <summary>Why the last check failed.</summary>
    public string LastVerifyError { get; set; }

    /// <summary>Day it expires.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Projection of the row (never the secret).
    /// </summary>
    public static Expression<Func<RegistryConnection, RegistryConnectionDto>> Projection { get; } = c => new RegistryConnectionDto
    {
        Id = c.Id,
        Kind = c.Kind,
        Fingerprint = c.Fingerprint,
        Status = c.Status,
        LastVerifiedDate = c.LastVerifiedDate,
        LastVerifyError = c.LastVerifyError,
        ExpiryDate = c.ExpiryDate,
    };
}

/// <summary>
/// The release settings of a package or a group, with what a release would start from.
/// </summary>
public class ReleaseSettingsDto
{
    /// <summary>Package, for package settings.</summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>Group, for group settings (or the group a package's settings come from).</summary>
    public int? PackageGroupId { get; set; }

    /// <summary>Name of the group.</summary>
    public string PackageGroupName { get; set; }

    /// <summary>Whether the package has no settings of its own and takes its group's.</summary>
    public bool FromGroup { get; set; }

    /// <summary>The settings; null until set.</summary>
    public ReleaseSettings Settings { get; set; }

    /// <summary>Problems of the settings (message keys); empty when a release can be planned.</summary>
    public List<string> Problems { get; set; } = [];

    /// <summary>Current version (the package's latest, or the group's highest).</summary>
    public string CurrentVersion { get; set; }

    /// <summary>Next versions.</summary>
    public List<VersionSuggestionDto> NextVersions { get; set; } = [];

    /// <summary>Whether the organization has a GitHub token for releases.</summary>
    public bool GitHubConnected { get; set; }
}

/// <summary>
/// A version a release can take.
/// </summary>
public class VersionSuggestionDto
{
    /// <summary><c>patch</c>, <c>minor</c>, <c>major</c> or <c>prerelease</c>.</summary>
    public string Kind { get; set; }

    /// <summary>The version.</summary>
    public string Version { get; set; }
}

/// <summary>
/// Suggested release settings read from a repository's workflows.
/// </summary>
public class ReleaseSuggestionDto
{
    /// <summary>The settings.</summary>
    public ReleaseSettings Settings { get; set; }

    /// <summary>Why they are suggested.</summary>
    public string Reason { get; set; }
}

/// <summary>
/// A release or operation plan.
/// </summary>
public class RegistryPlanDto
{
    /// <summary>Plan.</summary>
    public long Id { get; set; }

    /// <summary>Package of a package plan.</summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>Group of a group release.</summary>
    public int? PackageGroupId { get; set; }

    /// <summary>What it does.</summary>
    public RegistryPlanKind Kind { get; set; }

    /// <summary>State.</summary>
    public ChangePlanStatus Status { get; set; }

    /// <summary>One line summary.</summary>
    public string Title { get; set; }

    /// <summary>Version it releases or acts on.</summary>
    public string Version { get; set; }

    /// <summary>Version before the release.</summary>
    public string CurrentVersion { get; set; }

    /// <summary>Tag it pushes.</summary>
    public string Tag { get; set; }

    /// <summary>Text the approver types.</summary>
    public string ConfirmText { get; set; }

    /// <summary>Permission the approver needs.</summary>
    public OrgPermission RequiredPermission { get; set; }

    /// <summary>Release settings it was made with.</summary>
    public ReleaseSettings Release { get; set; }

    /// <summary>File changes of a version bump.</summary>
    public List<ReleaseFileChange> Files { get; set; } = [];

    /// <summary>Packages it covers.</summary>
    public List<int> PackageIds { get; set; } = [];

    /// <summary>Inputs of a workflow dispatch.</summary>
    public Dictionary<string, string> DispatchInputs { get; set; } = [];

    /// <summary>Branch head it was made on.</summary>
    public string BaseSha { get; set; }

    /// <summary>Deprecation message or dist-tag of an operation.</summary>
    public string Argument { get; set; }

    /// <summary>UTC instant after which it can no longer be approved.</summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>Who planned it.</summary>
    public string CreatedByUserName { get; set; }

    /// <summary>UTC instant it was planned.</summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>Who decided it.</summary>
    public string DecidedByUserName { get; set; }

    /// <summary>UTC instant of the decision.</summary>
    public DateTime? DecisionDate { get; set; }

    /// <summary>The job that writes it.</summary>
    public long? StoreJobId { get; set; }

    /// <summary>Commit of the version bump.</summary>
    public string CommitSha { get; set; }

    /// <summary>CI run id.</summary>
    public long? RunId { get; set; }

    /// <summary>CI run address.</summary>
    public string RunUrl { get; set; }

    /// <summary>CI run status.</summary>
    public string RunStatus { get; set; }

    /// <summary>CI run conclusion.</summary>
    public string RunConclusion { get; set; }

    /// <summary>Where a started release stands.</summary>
    public ReleaseFollowStatus? FollowStatus { get; set; }

    /// <summary>UTC instant the release was started.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>UTC instant the follow ended.</summary>
    public DateTime? FinishedDate { get; set; }

    /// <summary>Why writing or following failed.</summary>
    public string Error { get; set; }

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<RegistryPlan, RegistryPlanDto>> Projection { get; } = p => new RegistryPlanDto
    {
        Id = p.Id,
        TrackedPackageId = p.TrackedPackageId,
        PackageGroupId = p.PackageGroupId,
        Kind = p.Kind,
        Status = p.Status,
        Title = p.Title,
        Version = p.Version,
        CurrentVersion = p.CurrentVersion,
        Tag = p.Tag,
        ConfirmText = p.ConfirmText,
        RequiredPermission = p.RequiredPermission,
        Release = p.Release,
        Files = p.Files,
        PackageIds = p.PackageIds,
        DispatchInputs = p.DispatchInputs,
        BaseSha = p.BaseSha,
        Argument = p.Argument,
        ExpiryDate = p.ExpiryDate,
        CreatedByUserName = p.CreatedByUserName,
        CreationDate = p.CreationDate,
        DecidedByUserName = p.DecidedByUserName,
        DecisionDate = p.DecisionDate,
        StoreJobId = p.StoreJobId,
        CommitSha = p.CommitSha,
        RunId = p.RunId,
        RunUrl = p.RunUrl,
        RunStatus = p.RunStatus,
        RunConclusion = p.RunConclusion,
        FollowStatus = p.FollowStatus,
        StartedDate = p.StartedDate,
        FinishedDate = p.FinishedDate,
        Error = p.Error,
    };

    /// <summary>
    /// The plan as a DTO (after a write in memory).
    /// </summary>
    public static RegistryPlanDto From(RegistryPlan plan) => _compiled.Value(plan);

    private static readonly Lazy<Func<RegistryPlan, RegistryPlanDto>> _compiled = new(() => Projection.Compile());
}
