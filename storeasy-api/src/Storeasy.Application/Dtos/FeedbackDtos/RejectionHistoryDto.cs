namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// The App Store submission history of an app: every version storeasy saw with App Review, rejected or not.
/// </summary>
public class RejectionHistoryDto
{
    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>Submissions, oldest first.</summary>
    public List<SubmissionHistoryItemDto> Submissions { get; set; } = [];

    /// <summary>Submissions seen.</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Rejections among them.</summary>
    public int RejectionCount { get; set; }

    /// <summary>Guidelines cited in more than one rejection.</summary>
    public List<string> RepeatedGuidelines { get; set; } = [];
}

/// <summary>
/// One version in the submission history.
/// </summary>
public class SubmissionHistoryItemDto
{
    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>Version.</summary>
    public string VersionString { get; set; }

    /// <summary>Build number.</summary>
    public string BuildNumber { get; set; }

    /// <summary>Last known state.</summary>
    public string State { get; set; }

    /// <summary>UTC instant first seen with App Review.</summary>
    public DateTime? SubmittedDate { get; set; }

    /// <summary>Rejection cases of the version, oldest first.</summary>
    public List<RejectionCaseListDto> Rejections { get; set; } = [];
}
