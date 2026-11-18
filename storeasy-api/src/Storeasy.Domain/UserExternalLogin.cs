using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain;

/// <summary>
/// A link between a user and an external identity (the provider's stable subject id). Created on the first external
/// sign in, either with a new account or with the existing account of the same verified e-mail; later sign ins find the
/// user by <see cref="Subject"/> alone, so a changed e-mail at the provider keeps working.
/// </summary>
[Table(TableNames.UserExternalLogins)]
[Index(nameof(Provider), nameof(Subject), IsUnique = true)]
[Index(nameof(UserId))]
public class UserExternalLogin : CreationAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Subject"/>.
    /// </summary>
    public const int SubjectMaxLength = 255;

    /// <summary>
    /// The linked user.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Identity provider.
    /// </summary>
    public ExternalLoginProvider Provider { get; set; }

    /// <summary>
    /// The provider's subject id (<c>sub</c>).
    /// </summary>
    [MaxLength(SubjectMaxLength)]
    public string Subject { get; set; }

    /// <summary>
    /// E-mail the provider reported at the last sign in.
    /// </summary>
    [MaxLength(256)]
    public string Email { get; set; }

    /// <summary>
    /// UTC instant of the last sign in through the provider.
    /// </summary>
    public DateTime? LastLoginDate { get; set; }

    /// <summary>
    /// Navigation property of the user.
    /// </summary>
    public virtual User User { get; set; }
}
