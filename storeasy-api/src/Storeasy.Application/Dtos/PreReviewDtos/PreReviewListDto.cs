namespace Storeasy.Application.Dtos.PreReviewDtos;

/// <summary>
/// A pre-review in the history of a store record.
/// </summary>
public class PreReviewListDto
{
    /// <summary>
    /// Pre-review id.
    /// </summary>
    public long Id { get; set; }

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
    /// Expected decision; null until it succeeds.
    /// </summary>
    public PreReviewVerdict? Verdict { get; set; }

    /// <summary>
    /// Estimated chance of approval, 0 to 100; null until it succeeds.
    /// </summary>
    public int? ApprovalLikelihood { get; set; }

    /// <summary>
    /// Model asked.
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Blocker findings.
    /// </summary>
    public int BlockerCount { get; set; }

    /// <summary>
    /// Major findings.
    /// </summary>
    public int MajorCount { get; set; }

    /// <summary>
    /// Minor findings.
    /// </summary>
    public int MinorCount { get; set; }

    /// <summary>
    /// Estimated cost in US dollars.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// UTC instant it was started.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Who started it.
    /// </summary>
    public string CreatedByUserName { get; set; }
}
