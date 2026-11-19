namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the AI reply drafts (configuration section <see cref="SectionName"/>). The Anthropic API key is not here: each
/// organization saves its own (organizations/ai-settings), sealed in the database.
/// </summary>
public class AiOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Ai";

    /// <summary>
    /// Default model: Claude Haiku 5.5, the lowest cost current model.
    /// </summary>
    public const string DefaultModel = "claude-haiku-5-5";

    /// <summary>
    /// Model id of the reply drafts.
    /// </summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// Upper bound of the answer in tokens (thinking included).
    /// </summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>
    /// Effort of the model (<c>low</c>, <c>medium</c>, <c>high</c>, <c>max</c>); a short reply needs <c>low</c>.
    /// </summary>
    public string Effort { get; set; } = "low";

    /// <summary>
    /// Timeout of one call to Anthropic, in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
