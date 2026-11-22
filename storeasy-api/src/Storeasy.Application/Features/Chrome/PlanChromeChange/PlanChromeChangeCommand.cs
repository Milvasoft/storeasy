using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Chrome.PlanChromeChange;

/// <summary>
/// Plans a Chrome Web Store write: upload an extension package intake received and submit the item (for review, staged,
/// or skipping the review where the store allows it, with an initial deploy percentage), publish a staged revision, raise
/// the deploy percentage of the published revision, cancel the pending submission, or roll back. The live status is read
/// first and the package's manifest checks are run again against it. Dry run by default; approving the plan writes.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanChromeChangeCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Chrome store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// What to write.
    /// </summary>
    public ChromeChangeKind Kind { get; set; } = ChromeChangeKind.Publish;

    /// <summary>
    /// Publish: intake session of the extension ZIP to upload first; null submits the package uploaded last.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// Publish: stage the revision after approval instead of publishing it.
    /// </summary>
    public bool Staged { get; set; }

    /// <summary>
    /// Publish: initial deploy percentage (null keeps the dashboard's); deploy percentage: the new, higher one.
    /// </summary>
    public int? DeployPercentage { get; set; }

    /// <summary>
    /// Publish: ask the store to skip the review (it refuses for items that do not qualify).
    /// </summary>
    public bool SkipReview { get; set; }

    /// <summary>
    /// Rollback: earlier published version to publish again; null for the last one.
    /// </summary>
    public string TargetVersion { get; set; }

    /// <summary>
    /// Rollback: version number given to it, higher than the published one.
    /// </summary>
    public string NewVersion { get; set; }

    /// <summary>
    /// Rollback: reason, kept by the store.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// True (default): only save the plan for approval.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Confirmation text when the plan is approved at once (the extension id and the version).
    /// </summary>
    public string Confirm { get; set; }
}
