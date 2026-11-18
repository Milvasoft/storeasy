namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Plan of a Microsoft Store change (dry run): the diff, remarks and store calls, and the fingerprint of the store content it was made from. Applying sends the same change with <see cref="BaseHash"/>; the job reads the store again and refuses when the content changed meanwhile.
/// </summary>
public class MsChangePlanDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// What the change writes.
    /// </summary>
    public MsChangeKind Kind { get; set; }

    /// <summary>
    /// Submission target the change writes to.
    /// </summary>
    public MsSubmissionTarget Target { get; set; }

    /// <summary>
    /// Submission the content was read from (pending, or last published when a new one will be created).
    /// </summary>
    public string SubmissionId { get; set; }

    /// <summary>
    /// Whether applying creates a new submission (a copy of the last published one).
    /// </summary>
    public bool CreatesSubmission { get; set; }

    /// <summary>
    /// Field level diff.
    /// </summary>
    public List<MsPlanItemDto> Items { get; set; } = [];

    /// <summary>
    /// Non blocking remarks.
    /// </summary>
    public List<MsPlanWarningDto> Warnings { get; set; } = [];

    /// <summary>
    /// Store calls of the apply job.
    /// </summary>
    public List<MsPlanCallDto> Calls { get; set; } = [];

    /// <summary>
    /// Fingerprint of the store content the plan was made from.
    /// </summary>
    public string BaseHash { get; set; }

    /// <summary>
    /// Permission applying needs.
    /// </summary>
    public OrgPermission RequiredPermission { get; set; }

    /// <summary>
    /// Whether the change cannot be applied now (see <see cref="BlockKey"/>).
    /// </summary>
    public bool Blocked { get; set; }

    /// <summary>
    /// Message key of the reason when blocked.
    /// </summary>
    public string BlockKey { get; set; }

    /// <summary>
    /// Set when the change belongs to Partner Center (assisted step).
    /// </summary>
    public MsAssistedReason? AssistedReason { get; set; }

    /// <summary>
    /// Whether this was only a plan.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Apply job, when applied.
    /// </summary>
    public long? JobId { get; set; }

    /// <summary>
    /// The saved change plan (approve it through the shared plan endpoints, or apply through <c>ms/change</c>).
    /// </summary>
    public long? PlanId { get; set; }
}
