using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.VerifyEmail;

/// <summary>
/// Marks the account's e-mail address as verified with the token from the verification e-mail. Anonymous: the link may
/// be opened on a device that is not signed in.
/// </summary>
public record VerifyEmailCommand : ICommand
{
    /// <summary>
    /// Token from the verification link.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }
}
