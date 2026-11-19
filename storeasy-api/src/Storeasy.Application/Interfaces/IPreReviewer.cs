using Storeasy.Application.Utils.Ai;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reviews a submission snapshot with a language model the way the store's reviewer would (Claude through the Anthropic
/// API, with the organization's own key) and returns a structured report. It only judges: nothing is saved or submitted
/// here, and the raw answer of the provider is never passed on.
/// </summary>
public interface IPreReviewer
{
    /// <summary>
    /// Runs one pre-review. Failures (key, limits, refusals, an answer that is not a valid report) come back as
    /// <see cref="PreReviewResult.Outcome"/>.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PreReviewResult> ReviewAsync(PreReviewRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads the screenshots chosen for a pre-review and scales them down for the model. Images that cannot be read are
/// left out; the review goes on without them.
/// </summary>
public interface IPreReviewImageLoader
{
    /// <summary>
    /// Loads <paramref name="candidates"/> in order, at most <paramref name="max"/> of them.
    /// </summary>
    /// <param name="candidates"></param>
    /// <param name="max"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<PreReviewImage>> LoadAsync(IReadOnlyList<PreReviewImageCandidate> candidates, int max, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the model is given for one pre-review.
/// </summary>
/// <param name="ApiKey">The organization's Anthropic API key.</param>
/// <param name="Model">Model id.</param>
/// <param name="Store">Store whose reviewer is imitated.</param>
/// <param name="Language">Culture name of the report language (e.g. tr-TR).</param>
/// <param name="RulesBlock">The store's rules from the catalog (<see cref="PreReviewPrompt.RulesBlock"/>).</param>
/// <param name="Snapshot">The submission snapshot.</param>
/// <param name="Images">Screenshots, in the order of their labels.</param>
public sealed record PreReviewRequest(string ApiKey,
                                      string Model,
                                      StoreKind Store,
                                      string Language,
                                      string RulesBlock,
                                      PreReviewSnapshot Snapshot,
                                      IReadOnlyList<PreReviewImage> Images)
{
    /// <summary>
    /// Masks the key so the record never prints it.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"PreReviewRequest {{ Model = {this.Model}, Store = {this.Store}, Images = {this.Images?.Count} }}";
}

/// <summary>
/// A screenshot ready for the model.
/// </summary>
/// <param name="Label">How the prompt refers to it (e.g. <c>image 2: APP_IPHONE_67, en-US, position 2</c>).</param>
/// <param name="MediaType">MIME type of <paramref name="Data"/> (image/jpeg).</param>
/// <param name="Data">Image bytes.</param>
/// <param name="Checksum">The store's checksum of the original, when known.</param>
public sealed record PreReviewImage(string Label, string MediaType, byte[] Data, string Checksum);

/// <summary>
/// Result of <see cref="IPreReviewer.ReviewAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Report">The report when <see cref="PreReviewOutcome.Reviewed"/>; otherwise null.</param>
/// <param name="Model">Model that answered (or was asked).</param>
/// <param name="Usage">Tokens billed.</param>
public sealed record PreReviewResult(PreReviewOutcome Outcome, PreReviewReport Report, string Model, AiUsage Usage)
{
    /// <summary>
    /// A failed result without a report.
    /// </summary>
    /// <param name="outcome"></param>
    /// <param name="model"></param>
    /// <param name="usage"></param>
    /// <returns></returns>
    public static PreReviewResult Failure(PreReviewOutcome outcome, string model, AiUsage usage = null) => new(outcome, null, model, usage ?? AiUsage.None);
}

/// <summary>
/// Tokens of one call.
/// </summary>
/// <param name="InputTokens">Uncached input tokens.</param>
/// <param name="OutputTokens">Output tokens (thinking included).</param>
/// <param name="CacheReadInputTokens">Input tokens read from the prompt cache.</param>
/// <param name="CacheCreationInputTokens">Input tokens written to the prompt cache.</param>
public sealed record AiUsage(long InputTokens, long OutputTokens, long CacheReadInputTokens, long CacheCreationInputTokens)
{
    /// <summary>
    /// No tokens (the call failed before an answer).
    /// </summary>
    public static AiUsage None { get; } = new(0, 0, 0, 0);
}

/// <summary>
/// Outcome of a pre-review call.
/// </summary>
public enum PreReviewOutcome
{
    /// <summary>
    /// A valid report came back.
    /// </summary>
    Reviewed = 1,

    /// <summary>
    /// The provider rejected the key (401 or 403).
    /// </summary>
    KeyRejected = 2,

    /// <summary>
    /// The provider's rate limit was reached (429).
    /// </summary>
    RateLimited = 3,

    /// <summary>
    /// The provider is overloaded, failing or unreachable (5xx, 529, network, timeout).
    /// </summary>
    Unavailable = 4,

    /// <summary>
    /// The model declined to answer (stop reason <c>refusal</c>), also after the fallback.
    /// </summary>
    Refused = 5,

    /// <summary>
    /// Anything else: a request the provider refused, an answer cut short, an answer that is not a valid report.
    /// </summary>
    Failed = 6,
}
