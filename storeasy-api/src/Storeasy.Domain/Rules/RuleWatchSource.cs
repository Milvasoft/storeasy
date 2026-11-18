using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Rules;

/// <summary>
/// An official page the rule watcher reads (docs/urun-plani.md 12.3). The normalized text of the last read is kept to diff
/// the next one; a change opens a <see cref="RuleProposal"/>. Platform wide (no organization).
/// </summary>
[Table(TableNames.RuleSources)]
[Index(nameof(Url), IsUnique = true)]
public class RuleWatchSource : BaseEntity<int>
{
    /// <summary>
    /// Longest normalized text kept.
    /// </summary>
    public const int ContentMaxLength = 200000;

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    public string Name { get; set; }

    /// <summary>
    /// Store the source is about.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Address (an allow-listed official domain).
    /// </summary>
    [MaxLength(1000)]
    public string Url { get; set; }

    /// <summary>
    /// Whether the watcher reads it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// State of the last read.
    /// </summary>
    public RuleSourceStatus Status { get; set; } = RuleSourceStatus.New;

    /// <summary>
    /// UTC instant of the last read attempt.
    /// </summary>
    public DateTime? LastFetchDate { get; set; }

    /// <summary>
    /// UTC instant the content last changed.
    /// </summary>
    public DateTime? LastChangeDate { get; set; }

    /// <summary>
    /// SHA-256 of the current normalized text.
    /// </summary>
    [MaxLength(64)]
    public string ContentHash { get; set; }

    /// <summary>
    /// SHA-256 of the text before the last change.
    /// </summary>
    [MaxLength(64)]
    public string PreviousContentHash { get; set; }

    /// <summary>
    /// Normalized text of the last read.
    /// </summary>
    [MaxLength(ContentMaxLength)]
    public string Content { get; set; }

    /// <summary>
    /// Redacted reason of the last failed read.
    /// </summary>
    [MaxLength(500)]
    public string LastError { get; set; }
}
