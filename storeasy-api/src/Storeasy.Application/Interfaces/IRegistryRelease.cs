using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Registries;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// A file read from a GitHub repository.
/// </summary>
/// <param name="Path">Path.</param>
/// <param name="Sha">Blob sha.</param>
/// <param name="Content">Text.</param>
public sealed record GitHubFile(string Path, string Sha, string Content);

/// <summary>
/// A GitHub Actions workflow run.
/// </summary>
/// <param name="Id">Run id.</param>
/// <param name="Path">Workflow path (<c>.github/workflows/x.yml</c>).</param>
/// <param name="Event">Event that started it.</param>
/// <param name="Status"><c>queued</c>, <c>in_progress</c>, <c>completed</c>, ...</param>
/// <param name="Conclusion"><c>success</c>, <c>failure</c>, ... once completed.</param>
/// <param name="HeadSha">Commit it runs on.</param>
/// <param name="HtmlUrl">Address of the run.</param>
public sealed record GitHubRun(long Id, string Path, string Event, string Status, string Conclusion, string HeadSha, string HtmlUrl);

/// <summary>
/// The GitHub REST calls of releases started from storeasy, with a fine-grained personal access token (Contents read and
/// write, Actions read and write, Metadata read). A failed call throws <see cref="StoreCallException"/>.
/// </summary>
public interface IRegistryGitHub
{
    /// <summary>The login of the token's user (<c>GET /user</c>).</summary>
    Task<string> GetLoginAsync(string token, CancellationToken cancellationToken);

    /// <summary>A file at <paramref name="reference"/>; null when it does not exist.</summary>
    Task<GitHubFile> GetFileAsync(string token, string repository, string path, string reference, CancellationToken cancellationToken);

    /// <summary>The names of the files in <c>.github/workflows</c>.</summary>
    Task<List<string>> ListWorkflowFilesAsync(string token, string repository, string reference, CancellationToken cancellationToken);

    /// <summary>The head commit of <paramref name="branch"/>.</summary>
    Task<string> GetBranchShaAsync(string token, string repository, string branch, CancellationToken cancellationToken);

    /// <summary>Whether the tag exists.</summary>
    Task<bool> TagExistsAsync(string token, string repository, string tag, CancellationToken cancellationToken);

    /// <summary>Commits <paramref name="files"/> to <paramref name="branch"/> in one commit; returns the commit sha.</summary>
    Task<string> CommitFilesAsync(string token, string repository, string branch, IReadOnlyList<(string Path, string Sha, string Content)> files, string message, CancellationToken cancellationToken);

    /// <summary>Creates the lightweight tag <paramref name="tag"/> on <paramref name="sha"/>.</summary>
    Task CreateTagAsync(string token, string repository, string tag, string sha, CancellationToken cancellationToken);

    /// <summary>Starts <paramref name="workflow"/> with <c>workflow_dispatch</c>; returns the run when GitHub names it.</summary>
    Task<GitHubRun> DispatchAsync(string token, string repository, string workflow, string branch, IReadOnlyDictionary<string, string> inputs, CancellationToken cancellationToken);

    /// <summary>The runs on <paramref name="headSha"/>, newest first.</summary>
    Task<List<GitHubRun>> ListRunsAsync(string token, string repository, string headSha, CancellationToken cancellationToken);

    /// <summary>One run.</summary>
    Task<GitHubRun> GetRunAsync(string token, string repository, long runId, CancellationToken cancellationToken);
}

/// <summary>
/// Registry write operations with a stored API key (only from an approved plan). A failed call throws
/// <see cref="StoreCallException"/>.
/// </summary>
public interface IRegistryWriter
{
    /// <summary>
    /// Performs <paramref name="kind"/> on <paramref name="version"/> of <paramref name="name"/>.
    /// </summary>
    Task WriteAsync(RegistryPlanKind kind, PackageRegistry registry, string name, string version, string argument, string secret, CancellationToken cancellationToken);
}

/// <summary>
/// Release and operation plans of the package registries module: planning (with the exact changes), approval (permission
/// and typed confirmation), the write in a job and the follow of a started release until the version appears.
/// </summary>
public interface IRegistryPlanService
{
    /// <summary>Plans a release of a package or a group.</summary>
    Task<Response<RegistryPlanDto>> PlanReleaseAsync(int organizationId, int? trackedPackageId, int? packageGroupId, string version, CancellationToken cancellationToken);

    /// <summary>Plans a registry operation on a version of a package.</summary>
    Task<Response<RegistryPlanDto>> PlanOperationAsync(int organizationId, int trackedPackageId, RegistryPlanKind kind, string version, string argument, CancellationToken cancellationToken);

    /// <summary>Approves a pending plan (permission, typed confirmation, unchanged base) and queues its write.</summary>
    Task<Response<RegistryPlanDto>> ApproveAsync(int organizationId, long planId, string confirm, CancellationToken cancellationToken);

    /// <summary>Rejects a pending plan.</summary>
    Task<Response<RegistryPlanDto>> RejectAsync(int organizationId, long planId, CancellationToken cancellationToken);

    /// <summary>Suggests release settings from the repository's workflows.</summary>
    Task<Response<List<ReleaseSuggestionDto>>> DetectAsync(int organizationId, int? trackedPackageId, int? packageGroupId, string repository, CancellationToken cancellationToken);

    /// <summary>Writes an approved plan (the <see cref="StoreJobKind.RegistryPlanApply"/> job).</summary>
    Task<StoreJobOutcome> ApplyAsync(long planId, IStoreJobContext context, CancellationToken cancellationToken);

    /// <summary>Follows every started release: CI run status, the registries, timeouts. Returns the plans that changed.</summary>
    Task<int> FollowAsync(CancellationToken cancellationToken);
}
