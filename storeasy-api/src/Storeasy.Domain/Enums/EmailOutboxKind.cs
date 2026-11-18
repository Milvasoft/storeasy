using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What an e-mail outbox row holds. Values are explicit and never reused.
/// </summary>
public enum EmailOutboxKind : byte
{
    /// <summary>
    /// A rendered e-mail, sent as it is.
    /// </summary>
    [Description("E-mail")]
    Message = 1,

    /// <summary>
    /// A password reset request for an address: the sender looks the account up, issues the token and renders the mail,
    /// or drops the row when no account uses the address.
    /// </summary>
    [Description("Password reset request")]
    PasswordReset = 2,
}
