using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanCategories;

/// <summary>
/// Plans categories, copyright and content rights declaration of an App Store record (PUT categories): a change plan, applied when approved (or right away with DryRun false).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanCategoriesCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Primary category id; null keeps it.
    /// </summary>
    public string Primary { get; set; }

    /// <summary>
    /// Secondary category id; null keeps it, empty removes it.
    /// </summary>
    public string Secondary { get; set; }

    /// <summary>
    /// Copyright of the version; null keeps it.
    /// </summary>
    public string Copyright { get; set; }

    /// <summary>
    /// DOES_NOT_USE_THIRD_PARTY_CONTENT or USES_THIRD_PARTY_CONTENT; null keeps it.
    /// </summary>
    public string ContentRightsDeclaration { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
