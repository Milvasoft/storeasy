using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// AI settings of an organization. The API key itself is never returned: only whether one is set, when, by whom and its
/// last four characters.
/// </summary>
public class AiSettingsDto
{
    /// <summary>
    /// The settings with the models resolved from <paramref name="replyDraftModel"/> and <paramref name="preReviewModel"/>
    /// (the organization's choices, null for the defaults) and the list of models.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="replyDraftModel"></param>
    /// <param name="preReviewModel"></param>
    /// <returns></returns>
    public AiSettingsDto WithModels(AiOptions options, string replyDraftModel, string preReviewModel)
    {
        ReplyDraftModel = options.ReplyDraftModelFor(replyDraftModel);
        PreReviewModel = options.PreReviewModelFor(preReviewModel);
        Model = ReplyDraftModel;
        Models = [.. AiModelCatalog.All.Select(m => new AiModelOptionDto { Id = m.Id, Name = m.Name, InputUsdPerMTok = m.InputUsdPerMTok, OutputUsdPerMTok = m.OutputUsdPerMTok })];

        return this;
    }

    /// <summary>
    /// Whether an Anthropic API key is saved.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Last four characters of the key; null when none is set.
    /// </summary>
    public string ApiKeyLastFour { get; set; }

    /// <summary>
    /// UTC instant the key was saved; null when none is set.
    /// </summary>
    public DateTime? ApiKeySetDate { get; set; }

    /// <summary>
    /// Who saved the key; null when none is set.
    /// </summary>
    public string ApiKeySetByUserName { get; set; }

    /// <summary>
    /// Model the reply drafts are written with (same as <see cref="ReplyDraftModel"/>; kept for older clients).
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Model of the review reply drafts: the organization's choice, or the configured default (<c>Storeasy:Ai:Model</c>).
    /// </summary>
    public string ReplyDraftModel { get; set; }

    /// <summary>
    /// Model of the AI pre-reviews: the organization's choice, or the configured default (<c>Storeasy:Ai:PreReviewModel</c>).
    /// </summary>
    public string PreReviewModel { get; set; }

    /// <summary>
    /// Models that can be chosen, with their list prices.
    /// </summary>
    public List<AiModelOptionDto> Models { get; set; } = [];

    /// <summary>
    /// Only in the answer of saving a key: true when Anthropic accepted it, false when it could not be checked (the key
    /// is saved anyway); null otherwise.
    /// </summary>
    public bool? ApiKeyVerified { get; set; }
}
