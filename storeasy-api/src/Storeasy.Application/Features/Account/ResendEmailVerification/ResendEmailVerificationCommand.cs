using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.ResendEmailVerification;

/// <summary>
/// Sends a new verification e-mail to the signed in user; the links sent before stop working.
/// </summary>
public record ResendEmailVerificationCommand : ICommand
{
}
