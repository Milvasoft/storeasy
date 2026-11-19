namespace Storeasy.Application.Dtos.PreReviewDtos;

/// <summary>
/// An AI pre-review of a store record: the model's estimate of how that store's reviewer would decide, with the problems
/// found and the guideline each breaks. It is an estimate; it never blocks or performs a submission.
/// </summary>
public class PreReviewDto
{
    /// <summary>
    /// Pre-review id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Version reviewed, when known.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public PreReviewStatus Status { get; set; }

    /// <summary>
    /// Job that runs it.
    /// </summary>
    public long? JobId { get; set; }

    /// <summary>
    /// Model asked.
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Language of the report (culture name).
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Expected decision; null until it succeeds.
    /// </summary>
    public PreReviewVerdict? Verdict { get; set; }

    /// <summary>
    /// Estimated chance of approval, 0 to 100; null until it succeeds.
    /// </summary>
    public int? ApprovalLikelihood { get; set; }

    /// <summary>
    /// Short summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Problems found, most severe first.
    /// </summary>
    public List<PreReviewFindingDto> Findings { get; set; } = [];

    /// <summary>
    /// What a reviewer will test while running the app; check them before submitting.
    /// </summary>
    public List<string> RuntimeChecks { get; set; } = [];

    /// <summary>
    /// What is already good.
    /// </summary>
    public List<string> PositiveNotes { get; set; } = [];

    /// <summary>
    /// Screenshots sent as images.
    /// </summary>
    public int ImageCount { get; set; }

    /// <summary>
    /// Input tokens billed (uncached).
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// Output tokens billed.
    /// </summary>
    public long OutputTokens { get; set; }

    /// <summary>
    /// Input tokens read from the prompt cache.
    /// </summary>
    public long CacheReadInputTokens { get; set; }

    /// <summary>
    /// Input tokens written to the prompt cache.
    /// </summary>
    public long CacheCreationInputTokens { get; set; }

    /// <summary>
    /// Estimated cost in US dollars from the model's list prices.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// Message key of the failure, when it failed.
    /// </summary>
    public string ErrorKey { get; set; }

    /// <summary>
    /// The failure in the request's language, when it failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// UTC instant it was started.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Who started it.
    /// </summary>
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// UTC instant it finished.
    /// </summary>
    public DateTime? CompletedDate { get; set; }
}
