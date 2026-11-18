using Milvasoft.Components.CQRS.Command;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanListing;

/// <summary>
/// Plans listing text changes (App Store app info and version localizations, EULA; Play listings and app details): the store is read live, the field level diff is returned as a change plan. Values over the catalog limits (keywords counted in UTF-8 bytes) are refused with the field named as Fields[locale].field.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanListingCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// App Store version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Desired values.
    /// </summary>
    public List<ListingChangeDto> Changes { get; set; } = [];

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
