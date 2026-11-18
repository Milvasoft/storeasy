using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.SuggestMockupCaptions;

/// <summary>
/// Suggests the captions of a template in one locale from its other locales (docs 12.8). Behind the
/// <c>Studio.AiCaptions</c> kill switch and the configured translator: 503 when either is off. Nothing is saved.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SuggestMockupCaptionsCommand : ICommand<MockupCaptionSuggestionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Locale to write the suggestions in.
    /// </summary>
    public string TargetLocale { get; set; }

    /// <summary>
    /// Locales to translate from; null for the primary locale (and English when the template has it).
    /// </summary>
    public List<string> SourceLocales { get; set; }

    /// <summary>
    /// Slots to suggest; null for the slots whose caption in the target locale is empty.
    /// </summary>
    public List<int> SlotOrders { get; set; }
}
