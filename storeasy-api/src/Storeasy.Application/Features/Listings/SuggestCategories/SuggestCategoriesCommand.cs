using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Listings.SuggestCategories;

/// <summary>
/// Suggests the App Store primary and secondary category of a store record with the organization's AI key, chosen from
/// the store's own category list, from the listing texts of the primary language and the app's catalog profile. Nothing is
/// saved and nothing reaches the store: the member reviews the suggestion and plans it with categories (PUT). The audit
/// entry keeps the model, the tokens and the cost.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SuggestCategoriesCommand : ICommand<CategorySuggestionDto>, IOrganizationRequest
{
    /// <summary>
    /// Maximum length of <see cref="Instruction"/>.
    /// </summary>
    public const int InstructionMaxLength = 500;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Optional note for this suggestion, at most 500 characters.
    /// </summary>
    public string Instruction { get; set; }
}
