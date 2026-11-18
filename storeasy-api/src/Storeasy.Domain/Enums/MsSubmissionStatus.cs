using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Status of a Microsoft Store submission. The MSIX values mirror the API's status strings; <see cref="InProgress"/>, <see cref="Failed"/> and <see cref="Published"/> also carry the MSI/EXE publishing status.
/// </summary>
public enum MsSubmissionStatus : byte
{
    /// <summary>
    /// Draft: created and editable, not committed yet.
    /// </summary>
    [Description("Draft, not committed")]
    PendingCommit = 1,

    /// <summary>
    /// Committed; Partner Center is taking the changes in.
    /// </summary>
    [Description("Commit started")]
    CommitStarted = 2,

    /// <summary>
    /// The commit failed; the details carry the errors.
    /// </summary>
    [Description("Commit failed")]
    CommitFailed = 3,

    /// <summary>
    /// Packages and images are being processed.
    /// </summary>
    [Description("Pre-processing")]
    PreProcessing = 4,

    /// <summary>
    /// Processing of the packages or images failed.
    /// </summary>
    [Description("Pre-processing failed")]
    PreProcessingFailed = 5,

    /// <summary>
    /// In certification.
    /// </summary>
    [Description("In certification")]
    Certification = 6,

    /// <summary>
    /// Certification failed; the report is in Partner Center.
    /// </summary>
    [Description("Certification failed")]
    CertificationFailed = 7,

    /// <summary>
    /// Certified and being released.
    /// </summary>
    [Description("Releasing")]
    Release = 8,

    /// <summary>
    /// The release failed.
    /// </summary>
    [Description("Release failed")]
    ReleaseFailed = 9,

    /// <summary>
    /// Certified and waiting for the publish date or a manual publish.
    /// </summary>
    [Description("Waiting for publication")]
    PendingPublication = 10,

    /// <summary>
    /// Being published.
    /// </summary>
    [Description("Publishing")]
    Publishing = 11,

    /// <summary>
    /// Published.
    /// </summary>
    [Description("Published")]
    Published = 12,

    /// <summary>
    /// Publishing failed.
    /// </summary>
    [Description("Publish failed")]
    PublishFailed = 13,

    /// <summary>
    /// Canceled.
    /// </summary>
    [Description("Canceled")]
    Canceled = 14,

    /// <summary>
    /// MSI/EXE: publishing is in progress (INPROGRESS).
    /// </summary>
    [Description("In progress")]
    InProgress = 15,

    /// <summary>
    /// MSI/EXE: publishing failed (FAILED).
    /// </summary>
    [Description("Failed")]
    Failed = 16,

    /// <summary>
    /// The store answered a status storeasy does not know.
    /// </summary>
    [Description("Unknown")]
    Unknown = 17,
}
