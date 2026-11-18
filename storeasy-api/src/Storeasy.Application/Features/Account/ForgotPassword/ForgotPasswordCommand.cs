using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.ForgotPassword;

/// <summary>
/// Sends a password reset link to the e-mail address when an account uses it. The answer is the same whether or not an
/// account exists, so the endpoint cannot be used to find out who has an account.
/// </summary>
public record ForgotPasswordCommand : ICommand
{
    /// <summary>
    /// E-mail address of the account.
    /// </summary>
    public string Email { get; set; }
}
