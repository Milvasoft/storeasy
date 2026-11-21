namespace Storeasy.Application.Interfaces;

/// <summary>
/// GitHub calls of the winget channel: reads of microsoft/winget-pkgs (public, read with the owner's token for the higher
/// rate limit), the fork check, and the one write: a branch with the manifests pushed to the fork and the pull request
/// opened from it. Failures become <see cref="StoreCallException"/>.
/// </summary>
public interface IWingetClient
{
    /// <summary>
    /// Checks the token and the fork: the token works, the fork exists, is a fork of winget-pkgs and the token can push to it.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreVerification> VerifyAsync(string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// Version folders of a package in winget-pkgs, highest first; empty when the package has none.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="packageIdentifier"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<string>> ListVersionsAsync(string secretJson, string packageIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// Manifest files of one version folder (file name to YAML text).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="packageIdentifier"></param>
    /// <param name="version"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyDictionary<string, string>> ReadManifestFilesAsync(string secretJson, string packageIdentifier, string version, CancellationToken cancellationToken);

    /// <summary>
    /// Pull requests of the fork owner in winget-pkgs whose title names the package (newest first, open and closed).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="packageIdentifier"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<WingetPullRequestRead>> FindPullRequestsAsync(string secretJson, string packageIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// One pull request with its labels, the check runs of its head commit and its newest comments.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="number"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<WingetPullRequestRead> GetPullRequestAsync(string secretJson, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads an installer from a public address and returns its SHA-256 (upper case hex) and size.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<WingetInstallerHash> HashInstallerAsync(string url, CancellationToken cancellationToken);

    /// <summary>
    /// Brings the fork's default branch up to date with winget-pkgs, pushes a branch with the files in one commit and opens
    /// the pull request. When GitHub refuses the pull request (a fine-grained token cannot open one in a repository its
    /// owner does not own), the branch stays and the answer carries the compare page instead of a number.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="draft"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<WingetOpenedPullRequest> OpenPullRequestAsync(string secretJson, WingetPullRequestDraft draft, CancellationToken cancellationToken);
}

/// <summary>
/// A pull request as read from GitHub.
/// </summary>
public sealed class WingetPullRequestRead
{
    /// <summary>Number.</summary>
    public int Number { get; set; }

    /// <summary>Page.</summary>
    public string Url { get; set; }

    /// <summary>Title.</summary>
    public string Title { get; set; }

    /// <summary>Whether it is open.</summary>
    public bool Open { get; set; }

    /// <summary>Whether it was merged.</summary>
    public bool Merged { get; set; }

    /// <summary>Whether it is a draft.</summary>
    public bool Draft { get; set; }

    /// <summary>Head branch (in the fork).</summary>
    public string HeadBranch { get; set; }

    /// <summary>Head commit.</summary>
    public string HeadSha { get; set; }

    /// <summary>Labels.</summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>Check runs of the head commit (empty for a search result).</summary>
    public List<WingetCheck> Checks { get; set; } = [];

    /// <summary>Newest comments (empty for a search result).</summary>
    public List<WingetComment> Comments { get; set; } = [];

    /// <summary>Opened.</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>Merged.</summary>
    public DateTime? MergedDate { get; set; }

    /// <summary>Closed.</summary>
    public DateTime? ClosedDate { get; set; }
}

/// <summary>
/// What to push and open.
/// </summary>
/// <param name="Branch">Branch to create in the fork.</param>
/// <param name="CommitMessage">Commit message.</param>
/// <param name="Title">Pull request title.</param>
/// <param name="Body">Pull request body (Markdown).</param>
/// <param name="Files">Files to add or replace (path inside the repository to text).</param>
/// <param name="DeletePaths">Files to remove (a version rewritten from a singleton manifest).</param>
public sealed record WingetPullRequestDraft(string Branch, string CommitMessage, string Title, string Body, IReadOnlyList<Utils.Winget.WingetManifestFile> Files, IReadOnlyList<string> DeletePaths);

/// <summary>
/// The result of <see cref="IWingetClient.OpenPullRequestAsync"/>.
/// </summary>
/// <param name="Number">Pull request number; null when GitHub refused to open it.</param>
/// <param name="Url">Pull request page, or the compare page of the pushed branch.</param>
/// <param name="HeadSha">Commit pushed.</param>
/// <param name="RefusedReason">Why GitHub refused the pull request, when it did.</param>
public sealed record WingetOpenedPullRequest(int? Number, string Url, string HeadSha, string RefusedReason);

/// <summary>
/// Hash of a downloaded installer.
/// </summary>
/// <param name="Sha256">SHA-256, upper case hex (the form winget tools write).</param>
/// <param name="Size">Bytes read.</param>
public sealed record WingetInstallerHash(string Sha256, long Size);
