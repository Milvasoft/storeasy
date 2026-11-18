using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.GoogleSignIn;

/// <summary>
/// Completes a Google sign in with the code and state Google sent to the web app's callback page (anonymous). Signs in
/// the linked account; otherwise links the account whose e-mail Google verified, or creates one when the registration
/// mode allows it (an invitation token for the same address opens an invite only instance and joins the organization).
/// </summary>
public record GoogleSignInCommand : ICommand<LoginResponseDto>
{
    /// <summary>
    /// Authorization code from the callback.
    /// </summary>
    [LogMasked]
    public string Code { get; set; }

    /// <summary>
    /// State from the callback (the one <c>account/login/google/start</c> returned).
    /// </summary>
    [LogMasked]
    public string State { get; set; }

    /// <summary>
    /// Device id of the session, like the password login.
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// Invitation token when the sign in started from an invitation link.
    /// </summary>
    [LogMasked]
    public string InvitationToken { get; set; }
}
