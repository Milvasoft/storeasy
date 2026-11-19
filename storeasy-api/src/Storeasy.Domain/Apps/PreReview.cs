using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Apps;

/// <summary>
/// An AI pre-review of one store record: Claude reads a compact snapshot of the version being prepared (listing, screenshots,
/// declarations, review information, rules, past rejections) and judges it the way that store's reviewer would. It is an
/// estimate saved as history; it never blocks or performs a submission. No app content is kept here beyond the model's
/// report: the snapshot itself is only fingerprinted.
/// </summary>
[Table(TableNames.PreReviews)]
[Index(nameof(OrganizationId), nameof(AppId), nameof(Store), nameof(CreationDate))]
public class PreReview : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="Model"/>.
    /// </summary>
    public const int ModelMaxLength = 64;

    /// <summary>
    /// Column length of <see cref="Language"/>.
    /// </summary>
    public const int LanguageMaxLength = 16;

    /// <summary>
    /// Column length of <see cref="VersionString"/>.
    /// </summary>
    public const int VersionMaxLength = 64;

    /// <summary>
    /// Column length of <see cref="SnapshotHash"/> (lower case hex SHA-256).
    /// </summary>
    public const int HashLength = 64;

    /// <summary>
    /// Column length of <see cref="ErrorKey"/>.
    /// </summary>
    public const int ErrorKeyMaxLength = 64;

    /// <summary>
    /// Longest summary kept.
    /// </summary>
    public const int SummaryMaxLength = 4000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record reviewed.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store whose reviewer is imitated.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Version reviewed, when the store tells it.
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string VersionString { get; set; }

    /// <summary>
    /// Fingerprint of the snapshot sent to the model (texts and image checksums); equal hashes mean the same submission.
    /// </summary>
    [MaxLength(HashLength)]
    public string SnapshotHash { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public PreReviewStatus Status { get; set; }

    /// <summary>
    /// Job that runs it.
    /// </summary>
    public long? JobId { get; set; }

    /// <summary>
    /// Model asked (the organization's choice for pre-reviews).
    /// </summary>
    [MaxLength(ModelMaxLength)]
    public string Model { get; set; }

    /// <summary>
    /// Language of the report (culture name, e.g. tr-TR).
    /// </summary>
    [MaxLength(LanguageMaxLength)]
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
    /// Short summary of the report.
    /// </summary>
    [MaxLength(SummaryMaxLength)]
    public string Summary { get; set; }

    /// <summary>
    /// Problems found.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<PreReviewFinding> Findings { get; set; } = [];

    /// <summary>
    /// What a reviewer will test while running the app, to check before submitting.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> RuntimeChecks { get; set; } = [];

    /// <summary>
    /// What is already good.
    /// </summary>
    [Column(TypeName = "jsonb")]
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
    /// Output tokens billed (thinking included).
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
    [Column(TypeName = "numeric(12,6)")]
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// Message key of the failure, when it failed.
    /// </summary>
    [MaxLength(ErrorKeyMaxLength)]
    public string ErrorKey { get; set; }

    /// <summary>
    /// UTC instant it finished.
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    public virtual App App { get; set; }
}
