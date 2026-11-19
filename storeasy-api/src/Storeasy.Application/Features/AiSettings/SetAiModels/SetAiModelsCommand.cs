using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.AiSettings.SetAiModels;

/// <summary>
/// Chooses the model of each AI feature (review reply drafts, pre-reviews) from the fixed list of
/// <see cref="Utils.Models.AiModelCatalog"/>. Needs a saved key: the choice is kept with the key and removing the key resets
/// it. A field left null keeps its current choice.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record SetAiModelsCommand : ICommand<AiSettingsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Model of the review reply drafts; null keeps the current one.
    /// </summary>
    public string ReplyDraftModel { get; set; }

    /// <summary>
    /// Model of the AI pre-reviews; null keeps the current one.
    /// </summary>
    public string PreReviewModel { get; set; }
}
