using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Releases.PlanRelease;

/// <summary>
/// Plans a release: App Store release type and phased release of the version (SetReleaseType), or version codes on a Play track with status, staged rollout fraction, release notes and countries (AssignTrack). Production releases need ReleaseProduction and the typed confirmation to apply; test tracks ReleaseTest.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanReleaseCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// What to plan.
    /// </summary>
    public ReleasePlanKind Kind { get; set; }

    /// <summary>
    /// App Store version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// App Store release type.
    /// </summary>
    public ReleaseType? ReleaseType { get; set; }

    /// <summary>
    /// Release instant of a scheduled release (UTC).
    /// </summary>
    public DateTime? EarliestReleaseDate { get; set; }

    /// <summary>
    /// Release the App Store version in phases over 7 days.
    /// </summary>
    public bool? PhasedRelease { get; set; }

    /// <summary>
    /// Play track.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Play release name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Play version codes.
    /// </summary>
    public List<string> VersionCodes { get; set; } = [];

    /// <summary>
    /// Play release status: completed, draft or inProgress.
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
    /// Countries the release targets; null for every country of the track.
    /// </summary>
    public List<string> Countries { get; set; }

    /// <summary>
    /// Whether the rest of the world is included.
    /// </summary>
    public bool? IncludeRestOfWorld { get; set; }

    /// <summary>
    /// Typed confirmation for production (with DryRun false).
    /// </summary>
    public string Confirm { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
