using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Builds.PlanBundleUpload;

/// <summary>
/// Plans uploading an Android App Bundle (an uploaded asset) to Google Play with the resumable upload, and optionally putting it on a track in the same edit. A release to production needs ReleaseProduction and the typed confirmation to apply.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record PlanBundleUploadCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (Google Play).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Uploaded .aab asset.
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Track to put the bundle on (internal by default); empty only uploads.
    /// </summary>
    public string Track { get; set; } = "internal";

    /// <summary>
    /// Release status: completed, draft or inProgress.
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>
    /// Staged rollout fraction (0-1) of an inProgress release.
    /// </summary>
    public double? UserFraction { get; set; }

    /// <summary>
    /// Release notes per language.
    /// </summary>
    public List<ReleaseNote> ReleaseNotes { get; set; } = [];

    /// <summary>
    /// Typed confirmation for production (with DryRun false).
    /// </summary>
    public string Confirm { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
