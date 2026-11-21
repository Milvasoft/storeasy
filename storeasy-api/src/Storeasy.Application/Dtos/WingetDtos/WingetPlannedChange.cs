using Storeasy.Application.Utils.Winget;

namespace Storeasy.Application.Dtos.WingetDtos;

/// <summary>
/// The change of a winget plan (<see cref="ChangePlanKind.WingetChange"/>), stored in the plan: the exact manifest files
/// the approved write pushes, and the pull request around them. Installer hashes not known at planning are computed from
/// the downloads when the plan is applied (<see cref="WingetManifestValidator.PendingSha256"/>).
/// </summary>
public sealed class WingetPlannedChange
{
    /// <summary>
    /// What the pull request changes.
    /// </summary>
    public WingetChangeKind Kind { get; set; }

    /// <summary>
    /// Package identifier.
    /// </summary>
    public string PackageIdentifier { get; set; }

    /// <summary>
    /// Version the manifests are for.
    /// </summary>
    public string PackageVersion { get; set; }

    /// <summary>
    /// Version the plan was built on; null for a new package.
    /// </summary>
    public string BaseVersion { get; set; }

    /// <summary>
    /// Manifest files to push.
    /// </summary>
    public List<WingetManifestFile> Files { get; set; } = [];

    /// <summary>
    /// Files to remove (a singleton manifest replaced by a multi-file one).
    /// </summary>
    public List<string> DeletePaths { get; set; } = [];

    /// <summary>
    /// Installers whose hash is computed or checked on apply, by position in <c>Installers</c>.
    /// </summary>
    public List<WingetPlannedInstaller> Installers { get; set; } = [];

    /// <summary>
    /// Branch pushed to the fork.
    /// </summary>
    public string Branch { get; set; }

    /// <summary>
    /// Pull request title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Pull request body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Commit message.
    /// </summary>
    public string CommitMessage { get; set; }
}

/// <summary>
/// An installer entry of a planned installer manifest.
/// </summary>
/// <param name="Index">Position in the manifest's <c>Installers</c>.</param>
/// <param name="Url">Download address.</param>
/// <param name="ExpectedSha256">Hash given in the plan; null when it is computed on apply.</param>
public sealed record WingetPlannedInstaller(int Index, string Url, string ExpectedSha256);

/// <summary>
/// Payload of a <see cref="StoreJobKind.WingetSync"/> job.
/// </summary>
/// <param name="ConnectionId">winget connection to read with.</param>
/// <param name="StoreAppId">Only this package; null for every package of the connection.</param>
/// <param name="OpenOnly">Only refresh open pull requests (the schedule).</param>
public sealed record WingetSyncPayload(int ConnectionId, int? StoreAppId = null, bool OpenOnly = false);
