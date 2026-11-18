using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain;

/// <summary>
/// One e-mail waiting for the background sender. The payload (recipient, subject, body with any one time link) is sealed
/// with envelope encryption, so the database never holds a usable link in plain text. A row is deleted once it is sent or
/// its attempts are used up.
/// </summary>
[Table(TableNames.EmailOutbox)]
[Index(nameof(NextAttemptDate))]
public class EmailOutboxItem : BaseEntity<long>
{
    /// <summary>
    /// Column length of <see cref="LastError"/>.
    /// </summary>
    public const int ErrorMaxLength = 500;

    /// <summary>
    /// What the row holds.
    /// </summary>
    public EmailOutboxKind Kind { get; set; }

    /// <summary>
    /// Culture the mail is written in (e.g. tr-TR).
    /// </summary>
    [MaxLength(16)]
    public string Language { get; set; }

    /// <summary>
    /// Sealed JSON payload.
    /// </summary>
    public byte[] EncryptedPayload { get; set; }

    /// <summary>
    /// Data key of <see cref="EncryptedPayload"/>, sealed with the master key.
    /// </summary>
    public byte[] EncryptedDataKey { get; set; }

    /// <summary>
    /// Master key version that sealed <see cref="EncryptedDataKey"/>.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// Attempts made so far.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// UTC instant the next attempt is due.
    /// </summary>
    public DateTime NextAttemptDate { get; set; }

    /// <summary>
    /// UTC instant until which a sender holds the row; null while waiting.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// UTC instant the row was queued.
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// Error of the last failed attempt (redacted).
    /// </summary>
    [MaxLength(ErrorMaxLength)]
    public string LastError { get; set; }
}
