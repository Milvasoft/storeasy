using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanAgeRating;

/// <summary>
/// Plans age rating answers of an App Store record (PUT age-rating). Answers are the declaration's attributes with their JSON values ("NONE", true...); unknown attributes are refused (the new descriptors are assisted steps until the API has them).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanAgeRatingCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Attribute name to JSON value.
    /// </summary>
    public Dictionary<string, System.Text.Json.JsonElement> Answers { get; set; } = [];

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
