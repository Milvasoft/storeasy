using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.FirefoxDtos;

namespace Storeasy.Application.Features.Firefox.PlanFirefoxChange;

/// <summary>
/// Plans a Firefox Add-ons write: listing texts per locale, links and categories (<see cref="FirefoxChangeKind.Listing"/>);
/// a new version from an .xpi intake received, on the listed or unlisted channel, with release notes, notes to the reviewer,
/// license and source code (<see cref="FirefoxChangeKind.Version"/>); previews (<see cref="FirefoxChangeKind.Previews"/>);
/// the icon (<see cref="FirefoxChangeKind.Icon"/>); release notes of an existing version
/// (<see cref="FirefoxChangeKind.ReleaseNotes"/>). The add-on is read live first and every check runs against it. Dry run by
/// default; approving the plan writes.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanFirefoxChangeCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Firefox store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// What to write.
    /// </summary>
    public FirefoxChangeKind Kind { get; set; } = FirefoxChangeKind.Listing;

    /// <summary>
    /// Listing: values to write (field <c>Name</c>, <c>Summary</c>, <c>Description</c>, <c>Homepage</c>, <c>SupportUrl</c>,
    /// <c>SupportEmail</c>; an empty value removes the locale's value).
    /// </summary>
    public List<FirefoxListingValue> Listing { get; set; } = [];

    /// <summary>
    /// Listing: the new category slugs; null keeps them.
    /// </summary>
    public List<string> Categories { get; set; }

    /// <summary>
    /// Version: intake session of the .xpi to upload.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// Version: channel (listed: reviewed and shown on AMO; unlisted: signed for self distribution).
    /// </summary>
    public FirefoxVersionChannel Channel { get; set; } = FirefoxVersionChannel.Listed;

    /// <summary>
    /// Version and release notes: release notes per locale.
    /// </summary>
    public List<FirefoxLocalizedText> ReleaseNotes { get; set; } = [];

    /// <summary>
    /// Version: notes to the reviewer (build instructions, test account).
    /// </summary>
    public string ApprovalNotes { get; set; }

    /// <summary>
    /// Version: license slug (<c>MPL-2.0</c>, <c>MIT</c>, <c>all-rights-reserved</c>, ...); required for the first listed
    /// version, otherwise null keeps the previous one.
    /// </summary>
    public string License { get; set; }

    /// <summary>
    /// Version: applications (<c>firefox</c>, <c>android</c>); empty uses the manifest.
    /// </summary>
    public List<string> Compatibility { get; set; } = [];

    /// <summary>
    /// Version: asset of the source code archive (required by AMO for minified, bundled or transpiled code).
    /// </summary>
    public long? SourceAssetId { get; set; }

    /// <summary>
    /// Previews: assets (PNG or JPEG) to add, in order.
    /// </summary>
    public List<long> AddPreviewAssetIds { get; set; } = [];

    /// <summary>
    /// Previews: preview ids to remove.
    /// </summary>
    public List<long> DeletePreviewIds { get; set; } = [];

    /// <summary>
    /// Icon: asset of the new icon (square PNG or JPEG, 128 pixels recommended).
    /// </summary>
    public long? IconAssetId { get; set; }

    /// <summary>
    /// Release notes: the version (version string or id); null for the newest.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// True (default): only save the plan for approval.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Confirmation text when the plan is approved at once (the add-on guid, and the version for a version plan).
    /// </summary>
    public string Confirm { get; set; }
}
