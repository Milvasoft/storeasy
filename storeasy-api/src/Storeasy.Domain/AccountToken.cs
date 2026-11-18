using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain;

/// <summary>
/// A one time token sent to a user's e-mail address (verification, password reset). Only the SHA-256 hash is stored;
/// the token itself exists only in the e-mail. A token is used once (<see cref="UseDate"/>) and expires after
/// <see cref="LifetimeOf"/>. Issuing a new token of the same purpose retires the older ones.
/// </summary>
[Table(TableNames.AccountTokens)]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(UserId), nameof(Purpose))]
public class AccountToken : CreationAuditableEntity<long>
{
    /// <summary>
    /// How long an e-mail verification link stays valid.
    /// </summary>
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromHours(48);

    /// <summary>
    /// How long a password reset link stays valid.
    /// </summary>
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Owner of the token.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// What the token proves.
    /// </summary>
    public AccountTokenPurpose Purpose { get; set; }

    /// <summary>
    /// Hex SHA-256 hash of the token.
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; }

    /// <summary>
    /// UTC instant after which the token is rejected.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant the token was used or retired. Null while usable.
    /// </summary>
    public DateTime? UseDate { get; set; }

    /// <summary>
    /// Navigation property of the user.
    /// </summary>
    public virtual User User { get; set; }

    /// <summary>
    /// Lifetime of a token of <paramref name="purpose"/>.
    /// </summary>
    /// <param name="purpose"></param>
    /// <returns></returns>
    public static TimeSpan LifetimeOf(AccountTokenPurpose purpose) => purpose == AccountTokenPurpose.PasswordReset ? PasswordResetLifetime : EmailVerificationLifetime;
}
