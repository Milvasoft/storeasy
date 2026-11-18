namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// A rejection case with its text, checklist, reply draft and timeline.
/// </summary>
public class RejectionCaseDto
{
    /// <summary>Case id.</summary>
    public int Id { get; set; }

    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>App name.</summary>
    public string AppName { get; set; }

    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>Store.</summary>
    public StoreKind Store { get; set; }

    /// <summary>The store's id of the version.</summary>
    public string ExternalVersionId { get; set; }

    /// <summary>Version.</summary>
    public string VersionString { get; set; }

    /// <summary>Build number.</summary>
    public string BuildNumber { get; set; }

    /// <summary>What was rejected.</summary>
    public RejectionKind Kind { get; set; }

    /// <summary>State.</summary>
    public RejectionState State { get; set; }

    /// <summary>How it was opened.</summary>
    public RejectionSource OpenedVia { get; set; }

    /// <summary>UTC instant opened.</summary>
    public DateTime OpenedDate { get; set; }

    /// <summary>UTC instant closed.</summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>Why it was closed.</summary>
    public RejectionCloseReason? ClosedVia { get; set; }

    /// <summary>Note given when closed by hand.</summary>
    public string CloseNote { get; set; }

    /// <summary>App Store Connect page where the review message is (no API reads it).</summary>
    public string ResolutionCenterUrl { get; set; }

    /// <summary>Pasted review text.</summary>
    public string RawText { get; set; }

    /// <summary>Who pasted it.</summary>
    public string RawTextByUserName { get; set; }

    /// <summary>UTC instant pasted.</summary>
    public DateTime? RawTextDate { get; set; }

    /// <summary>Cited guidelines.</summary>
    public List<RejectionGuideline> Guidelines { get; set; }

    /// <summary>Guidelines of this case that an earlier case of the same app cited too.</summary>
    public List<string> RepeatedGuidelines { get; set; }

    /// <summary>Fix items.</summary>
    public List<RejectionChecklistItem> Checklist { get; set; }

    /// <summary>Reply draft.</summary>
    public RejectionReplyDraftDto ReplyDraft { get; set; }

    /// <summary>Earlier case of the same version.</summary>
    public int? PreviousCaseId { get; set; }

    /// <summary>What happened, in order.</summary>
    public List<RejectionTimelineEntry> Timeline { get; set; }
}

/// <summary>
/// The Resolution Center reply draft of a case.
/// </summary>
public class RejectionReplyDraftDto
{
    /// <summary>Text.</summary>
    public string Body { get; set; }

    /// <summary>Characters (line breaks count, as in the Resolution Center).</summary>
    public int Length { get; set; }

    /// <summary>Resolution Center limit.</summary>
    public int MaxLength { get; set; }

    /// <summary>Whether the draft is longer than the limit (it cannot be marked sent then).</summary>
    public bool OverLimit { get; set; }

    /// <summary>UTC instant saved last.</summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>Who saved it last.</summary>
    public string ByUserName { get; set; }

    /// <summary>Channel it was saved from.</summary>
    public ActionChannel? Channel { get; set; }

    /// <summary>UTC instant marked as sent.</summary>
    public DateTime? SentDate { get; set; }

    /// <summary>Who marked it as sent.</summary>
    public string SentByUserName { get; set; }
}
