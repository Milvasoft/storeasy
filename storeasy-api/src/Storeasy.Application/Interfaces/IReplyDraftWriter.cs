namespace Storeasy.Application.Interfaces;

/// <summary>
/// Writes a reply draft for a store review with a language model (Claude through the Anthropic API, with the
/// organization's own key). It only returns text: saving the draft and sending a reply are the caller's business, and a
/// reply never goes to a store without a person.
/// </summary>
public interface IReplyDraftWriter
{
    /// <summary>
    /// Writes one reply draft. Failures (key, limits, refusals) come back as <see cref="ReplyDraftResult.Outcome"/>; the
    /// raw answer of the provider is never passed on.
    /// </summary>
    Task<ReplyDraftResult> WriteAsync(ReplyDraftRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a key with a call that costs nothing (reading the model). Network failures and outages give
    /// <see cref="AiKeyCheck.Unverified"/>, never a rejection.
    /// </summary>
    Task<AiKeyCheck> CheckKeyAsync(string apiKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the model is told about one review.
/// </summary>
/// <param name="ApiKey">The organization's Anthropic API key.</param>
/// <param name="Store">Store of the review.</param>
/// <param name="AppName">Name of the app.</param>
/// <param name="Rating">Stars, 1 to 5.</param>
/// <param name="Title">Title (App Store only), if any.</param>
/// <param name="Body">Review text.</param>
/// <param name="Locale">Language of the review, if known.</param>
/// <param name="Territory">Storefront or country, if known.</param>
/// <param name="AppVersion">App version reviewed, if known.</param>
/// <param name="PreviousReply">The developer's published reply, if any (the draft replaces it).</param>
/// <param name="MaxLength">Longest reply the store accepts, in user perceived characters.</param>
public sealed record ReplyDraftRequest(string ApiKey,
                                       StoreKind Store,
                                       string AppName,
                                       int Rating,
                                       string Title,
                                       string Body,
                                       string Locale,
                                       string Territory,
                                       string AppVersion,
                                       string PreviousReply,
                                       int MaxLength);

/// <summary>
/// Result of <see cref="IReplyDraftWriter.WriteAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Text">The reply text when <see cref="ReplyDraftOutcome.Written"/>; otherwise null.</param>
/// <param name="Model">Model that answered (or was asked).</param>
/// <param name="InputTokens">Input tokens billed; 0 when unknown.</param>
/// <param name="OutputTokens">Output tokens billed; 0 when unknown.</param>
public sealed record ReplyDraftResult(ReplyDraftOutcome Outcome, string Text, string Model, long InputTokens = 0, long OutputTokens = 0)
{
    /// <summary>
    /// A failed result without text.
    /// </summary>
    public static ReplyDraftResult Failure(ReplyDraftOutcome outcome, string model) => new(outcome, null, model);
}

/// <summary>
/// Outcome of a reply draft.
/// </summary>
public enum ReplyDraftOutcome
{
    /// <summary>
    /// A reply was written.
    /// </summary>
    Written = 1,

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
    /// The model declined to answer (stop reason <c>refusal</c>).
    /// </summary>
    Refused = 5,

    /// <summary>
    /// Anything else: a request the provider refused, an answer cut short, no text.
    /// </summary>
    Failed = 6,
}

/// <summary>
/// Result of <see cref="IReplyDraftWriter.CheckKeyAsync"/>.
/// </summary>
public enum AiKeyCheck
{
    /// <summary>
    /// The provider accepted the key.
    /// </summary>
    Valid = 1,

    /// <summary>
    /// The provider rejected the key (401 or 403).
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// The key could not be checked (network, outage, rate limit).
    /// </summary>
    Unverified = 3,
}
