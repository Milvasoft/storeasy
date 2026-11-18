using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.StartGoogleSignIn;

/// <summary>
/// Starts a Google sign in (anonymous): returns Google's authorization address and the state the callback must bring
/// back. Answers <see cref="MessageKey.GoogleSignInDisabled"/> when sign in with Google is not configured.
/// </summary>
public record StartGoogleSignInCommand : ICommand<GoogleSignInStartDto>
{
}
