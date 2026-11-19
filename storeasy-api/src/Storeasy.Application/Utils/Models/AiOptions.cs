namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the AI features (configuration section <see cref="SectionName"/>): review reply drafts and pre-reviews. The
/// Anthropic API key is not here: each organization saves its own (organizations/ai-settings), sealed in the database,
/// together with its choice of model per feature from <see cref="AiModelCatalog"/>.
/// </summary>
public class AiOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Ai";

    /// <summary>
    /// Default model of the reply drafts: Claude Haiku 5.5, the lowest cost current model.
    /// </summary>
    public const string DefaultModel = AiModelCatalog.Haiku;

    /// <summary>
    /// Default model of the pre-reviews: Claude Sonnet 5.5.
    /// </summary>
    public const string DefaultPreReviewModel = AiModelCatalog.Sonnet;

    /// <summary>
    /// Default model of the reply drafts, when the organization has not chosen one.
    /// </summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// Upper bound of a reply draft in tokens (thinking included).
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Effort of the reply drafts (<c>low</c>, <c>medium</c>, <c>high</c>, <c>max</c>); a short reply needs <c>low</c>.
    /// </summary>
    public string Effort { get; set; } = "low";

    /// <summary>
    /// Timeout of one reply draft call to Anthropic, in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Default model of the pre-reviews, when the organization has not chosen one.
    /// </summary>
    public string PreReviewModel { get; set; } = DefaultPreReviewModel;

    /// <summary>
    /// Upper bound of a pre-review answer in tokens (thinking included).
    /// </summary>
    public int PreReviewMaxTokens { get; set; } = 16000;

    /// <summary>
    /// Effort of the pre-reviews by model id; a model not listed uses <c>medium</c>. Defaults: <c>low</c> for Claude Haiku
    /// 5.5, <c>medium</c> for Claude Sonnet 5.5 and Claude Opus 5.5.
    /// </summary>
    public Dictionary<string, string> PreReviewEffort { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [AiModelCatalog.Haiku] = "low",
        [AiModelCatalog.Sonnet] = "medium",
        [AiModelCatalog.Opus] = "medium",
    };

    /// <summary>
    /// Whether a pre-review on a model that supports it opts into the server-side refusal fallback
    /// (<c>fallbacks: "default"</c>, beta <c>server-side-fallback-2026-07-01</c>).
    /// </summary>
    public bool PreReviewFallback { get; set; } = true;

    /// <summary>
    /// Most screenshots sent as images in one pre-review.
    /// </summary>
    public int PreReviewMaxImages { get; set; } = 10;

    /// <summary>
    /// Longest edge of a screenshot sent to the model, in pixels (larger ones are scaled down).
    /// </summary>
    public int PreReviewImageMaxEdge { get; set; } = 1200;

    /// <summary>
    /// Timeout of one pre-review call to Anthropic, in seconds.
    /// </summary>
    public int PreReviewTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Effort of a pre-review on <paramref name="model"/>.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public string PreReviewEffortOf(string model) => model != null && PreReviewEffort.TryGetValue(model, out var effort) && !string.IsNullOrWhiteSpace(effort) ? effort : "medium";

    /// <summary>
    /// Model of the reply drafts: the organization's choice when it is in <see cref="AiModelCatalog"/>, otherwise
    /// <see cref="Model"/>.
    /// </summary>
    /// <param name="chosen"></param>
    /// <returns></returns>
    public string ReplyDraftModelFor(string chosen) => AiModelCatalog.IsKnown(chosen) ? chosen : Model;

    /// <summary>
    /// Model of the pre-reviews: the organization's choice when it is in <see cref="AiModelCatalog"/>, otherwise
    /// <see cref="PreReviewModel"/>.
    /// </summary>
    /// <param name="chosen"></param>
    /// <returns></returns>
    public string PreReviewModelFor(string chosen) => AiModelCatalog.IsKnown(chosen) ? chosen : PreReviewModel;
}
