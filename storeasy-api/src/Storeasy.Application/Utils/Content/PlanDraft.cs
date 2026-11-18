namespace Storeasy.Application.Utils.Content;

/// <summary>
/// A change plan before it is saved: what the planner computed from the store's current content and the desired values.
/// <see cref="BaseKeys"/> names the content keys whose current values are fingerprinted (<see cref="Domain.Content.ContentDiff.Hash"/>);
/// <see cref="ContentSnapshots.PartsOf"/> names what is read again when the plan is approved.
/// </summary>
public sealed class PlanDraft
{
    /// <summary>
    /// What the plan changes.
    /// </summary>
    public ChangePlanKind Kind { get; set; }

    /// <summary>
    /// One line description.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// What the apply job writes.
    /// </summary>
    public ContentChangeSet Changes { get; set; } = new();

    /// <summary>
    /// Field level diff.
    /// </summary>
    public List<PlanItem> Items { get; set; } = [];

    /// <summary>
    /// Non blocking remarks.
    /// </summary>
    public List<PlanWarning> Warnings { get; set; } = [];

    /// <summary>
    /// Store calls of the apply job.
    /// </summary>
    public List<PlanCall> Calls { get; set; } = [];

    /// <summary>
    /// Check results of uploaded files.
    /// </summary>
    public List<PlanFile> Files { get; set; } = [];

    /// <summary>
    /// Content keys whose current values the plan is based on.
    /// </summary>
    public List<string> BaseKeys { get; set; } = [];

    /// <summary>
    /// Play track the plan writes to, if one (decides the permission).
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// App Store version.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Version string (for the confirmation text).
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Permission the plan needs when the kind alone does not decide it (Microsoft Store changes); null for the kind's
    /// default.
    /// </summary>
    public OrgPermission? RequiredPermission { get; set; }

    /// <summary>
    /// Typed confirmation the planner asks for (e.g. a price change for existing subscribers); null: by kind.
    /// </summary>
    public string ConfirmText { get; set; }

    /// <summary>
    /// What the apply job writes when it is not a <see cref="ContentChangeSet"/> (a change plan extension's own set).
    /// </summary>
    public object CustomChanges { get; set; }

    /// <summary>
    /// Whether the plan writes nothing.
    /// </summary>
    public bool IsEmpty => Items.Count == 0 && Changes.IsEmpty;
}
