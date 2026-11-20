using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Listings.SuggestListingField;

/// <summary>
/// Suggests a value of one listing text (App Store name, subtitle, description, keywords, promotional text, What's New;
/// Play title, short and full description) with the organization's AI key, from the listing as the store has it, the
/// app's catalog profile and the field's catalog rules and limits. Nothing is saved and nothing reaches the store: the
/// member edits the suggestion and plans it with listings/plan. The audit entry keeps the model, the tokens and the cost.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SuggestListingFieldCommand : ICommand<ListingSuggestionDto>, IOrganizationRequest
{
    /// <summary>
    /// Maximum length of <see cref="Instruction"/>.
    /// </summary>
    public const int InstructionMaxLength = 500;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// App Store version whose texts are read (null: the editable version, as listings/get).
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Language of the listing (one the listing has).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Listing field key.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Optional note for this suggestion ("shorter", "mention offline mode"), at most 500 characters.
    /// </summary>
    public string Instruction { get; set; }
}
