using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One state a winget pull request was seen in, with the first time storeasy saw it.
/// </summary>
public class WingetStateChange
{
    /// <summary>
    /// State.
    /// </summary>
    public WingetPullRequestState State { get; set; }

    /// <summary>
    /// UTC instant storeasy first saw the state.
    /// </summary>
    public DateTime Date { get; set; }
}

/// <summary>
/// One check run on the head commit of a winget pull request (the validation pipeline reports its result as checks).
/// </summary>
public class WingetCheck
{
    /// <summary>
    /// Check name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// <c>queued</c>, <c>in_progress</c> or <c>completed</c>.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Result of a completed check (<c>success</c>, <c>failure</c>, <c>neutral</c>, <c>cancelled</c>, <c>timed_out</c>,
    /// <c>action_required</c>, <c>skipped</c>); null while it runs.
    /// </summary>
    public string Conclusion { get; set; }

    /// <summary>
    /// Page of the check run.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// A comment of the validation bot or a moderator on a winget pull request, shortened.
/// </summary>
public class WingetComment
{
    /// <summary>
    /// Maximum length of <see cref="Excerpt"/>.
    /// </summary>
    public const int ExcerptMaxLength = 600;

    /// <summary>
    /// GitHub login of the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Whether the author is a bot account.
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>
    /// Start of the comment text (HTML comments and badges removed).
    /// </summary>
    public string Excerpt { get; set; }

    /// <summary>
    /// Page of the comment.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// UTC instant the comment was written.
    /// </summary>
    public DateTime Date { get; set; }
}
