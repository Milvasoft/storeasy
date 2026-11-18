using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a one time account token proves. Values are explicit and never reused.
/// </summary>
public enum AccountTokenPurpose : byte
{
    /// <summary>
    /// The holder owns the e-mail address of the account.
    /// </summary>
    [Description("E-mail verification")]
    EmailVerification = 1,

    /// <summary>
    /// The holder may set a new password for the account.
    /// </summary>
    [Description("Password reset")]
    PasswordReset = 2,
}
