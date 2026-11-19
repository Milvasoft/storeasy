namespace Storeasy.Application.Utils.Models;

/// <summary>
/// The models an organization can choose for its AI features, with Anthropic's list prices in US dollars per million
/// tokens (MTok). The list is fixed on purpose: a small choice between cost and depth. Prices only feed the cost estimate
/// shown with a pre-review; Anthropic's invoice is the truth.
/// </summary>
public static class AiModelCatalog
{
    /// <summary>
    /// Claude Haiku 5.5.
    /// </summary>
    public const string Haiku = "claude-haiku-5-5";

    /// <summary>
    /// Claude Sonnet 5.5.
    /// </summary>
    public const string Sonnet = "claude-sonnet-5-5";

    /// <summary>
    /// Claude Opus 5.5.
    /// </summary>
    public const string Opus = "claude-opus-5-5";

    /// <summary>
    /// Every model that can be chosen, cheapest first.
    /// </summary>
    public static IReadOnlyList<AiModelInfo> All { get; } =
    [
        // Haiku 5.5 has a second rate card above 100K prompt tokens ($0.50 / $2.50).
        new(Haiku, "Claude Haiku 5.5", 0.10m, 0.50m, 0.01m, 0.125m, SupportsFallback: false, LongPromptThreshold: 100_000, LongInputUsdPerMTok: 0.50m, LongOutputUsdPerMTok: 2.50m),
        new(Sonnet, "Claude Sonnet 5.5", 2m, 10m, 0.10m, 2.50m, SupportsFallback: true),
        new(Opus, "Claude Opus 5.5", 4m, 20m, 0.20m, 5m, SupportsFallback: true),
    ];

    /// <summary>
    /// The model with <paramref name="id"/>, or null.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static AiModelInfo Find(string id) => All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Whether <paramref name="id"/> can be chosen.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool IsKnown(string id) => Find(id) != null;

    /// <summary>
    /// Estimated cost of one call in US dollars, rounded to six decimals. Unknown models cost 0. Cache reads and writes are
    /// priced separately from the uncached input tokens, as Anthropic bills them.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="inputTokens">Uncached input tokens.</param>
    /// <param name="outputTokens">Output tokens (thinking included).</param>
    /// <param name="cacheReadTokens">Input tokens read from the cache.</param>
    /// <param name="cacheWriteTokens">Input tokens written to the cache (5 minute lifetime).</param>
    /// <returns></returns>
    public static decimal EstimateCostUsd(string model, long inputTokens, long outputTokens, long cacheReadTokens = 0, long cacheWriteTokens = 0)
    {
        var info = Find(model);

        if (info == null)
            return 0m;

        var promptTokens = inputTokens + cacheReadTokens + cacheWriteTokens;
        var longPrompt = info.LongPromptThreshold > 0 && promptTokens > info.LongPromptThreshold;
        var inputPrice = longPrompt ? info.LongInputUsdPerMTok : info.InputUsdPerMTok;
        var outputPrice = longPrompt ? info.LongOutputUsdPerMTok : info.OutputUsdPerMTok;
        var cacheReadPrice = longPrompt ? inputPrice / 10m : info.CacheReadUsdPerMTok;
        var cacheWritePrice = longPrompt ? inputPrice * 1.25m : info.CacheWriteUsdPerMTok;

        var cost = (inputTokens * inputPrice + outputTokens * outputPrice + cacheReadTokens * cacheReadPrice + cacheWriteTokens * cacheWritePrice) / 1_000_000m;

        return Math.Round(cost, 6, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// A model of <see cref="AiModelCatalog"/>.
/// </summary>
/// <param name="Id">Model id sent to Anthropic.</param>
/// <param name="Name">Display name.</param>
/// <param name="InputUsdPerMTok">Input price per million tokens.</param>
/// <param name="OutputUsdPerMTok">Output price per million tokens.</param>
/// <param name="CacheReadUsdPerMTok">Price of a cache read per million tokens.</param>
/// <param name="CacheWriteUsdPerMTok">Price of a 5 minute cache write per million tokens.</param>
/// <param name="SupportsFallback">Whether the server-side refusal fallback is available on the model.</param>
/// <param name="LongPromptThreshold">Prompt size above which the long prompt prices apply; 0 when there is one rate card.</param>
/// <param name="LongInputUsdPerMTok">Input price above the threshold.</param>
/// <param name="LongOutputUsdPerMTok">Output price above the threshold.</param>
public sealed record AiModelInfo(string Id,
                                 string Name,
                                 decimal InputUsdPerMTok,
                                 decimal OutputUsdPerMTok,
                                 decimal CacheReadUsdPerMTok,
                                 decimal CacheWriteUsdPerMTok,
                                 bool SupportsFallback,
                                 long LongPromptThreshold = 0,
                                 decimal LongInputUsdPerMTok = 0,
                                 decimal LongOutputUsdPerMTok = 0);
