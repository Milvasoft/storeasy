using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.ResetPassword;

/// <summary>
/// Sets a new password with the token from the password reset e-mail. Every session and access token of the account
/// stops working, other reset links are retired, a lockout is cleared and the e-mail counts as verified (the link
/// reached its owner).
/// </summary>
public record ResetPasswordCommand : ICommand
{
    /// <summary>
    /// Token from the reset link.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }

    /// <summary>
    /// New password.
    /// </summary>
    [LogMasked]
    public string NewPassword { get; set; }
}
