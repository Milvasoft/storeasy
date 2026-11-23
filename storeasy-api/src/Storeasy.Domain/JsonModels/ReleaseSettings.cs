using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// How storeasy starts a release of a package or a group in its repository's CI.
/// </summary>
public class ReleaseSettings
{
    /// <summary>
    /// GitHub repository (<c>owner/repo</c>).
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Branch the release starts from (and the bump is committed to).
    /// </summary>
    public string Branch { get; set; } = "master";

    /// <summary>
    /// How the release starts.
    /// </summary>
    public ReleaseTrigger Trigger { get; set; }

    /// <summary>
    /// Files a version bump changes.
    /// </summary>
    public List<VersionFileSetting> VersionFiles { get; set; } = [];

    /// <summary>
    /// Tag to push, with <c>{version}</c> for the version (for example <c>v{version}</c>, <c>sdk-rust-v{version}</c>).
    /// </summary>
    public string TagPattern { get; set; }

    /// <summary>
    /// Workflow file that publishes (in <c>.github/workflows</c>): dispatched by <see cref="ReleaseTrigger.Dispatch"/>,
    /// and the run storeasy follows.
    /// </summary>
    public string Workflow { get; set; }

    /// <summary>
    /// Name of the dispatch input that takes the version.
    /// </summary>
    public string DispatchVersionInput { get; set; }

    /// <summary>
    /// Name of the dispatch input that takes whether the release is a prerelease; null when the workflow has none.
    /// </summary>
    public string DispatchPrereleaseInput { get; set; }

    /// <summary>
    /// Whether the dispatch version input takes the tag (<c>v1.2.3</c>) rather than the bare version.
    /// </summary>
    public bool DispatchTakesTag { get; set; }
}

/// <summary>
/// A version file of <see cref="ReleaseSettings"/>.
/// </summary>
public class VersionFileSetting
{
    /// <summary>
    /// Path in the repository.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Where the version is in the file.
    /// </summary>
    public VersionFileFormat Format { get; set; }
}

/// <summary>
/// One file change of a release plan: the file as the plan read it and as the release commits it.
/// </summary>
public class ReleaseFileChange
{
    /// <summary>
    /// Path in the repository.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Blob sha the plan read; approval refuses when the file changed since.
    /// </summary>
    public string BaseSha { get; set; }

    /// <summary>
    /// Version in the file before.
    /// </summary>
    public string OldVersion { get; set; }

    /// <summary>
    /// The changed line before.
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// The changed line after.
    /// </summary>
    public string After { get; set; }

    /// <summary>
    /// Line number of the change (1 based).
    /// </summary>
    public int Line { get; set; }
}
