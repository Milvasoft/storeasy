namespace Storeasy.Application.Interfaces;

/// <summary>
/// Issues the one time account tokens and sends the account e-mails (verification, password reset, invitation) in the
/// current request culture. Best effort: a delivery failure is logged and reported as false, never thrown, so the
/// calling flow (registration, forgot password, invitation) completes and the user can ask for the mail again.
/// </summary>
public interface IAccountMailer
{
    /// <summary>
    /// Retires the user's unused verification tokens, issues a new one and mails the verification link.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Whether the e-mail was handed to the provider.</returns>
    Task<bool> SendEmailVerificationAsync(int userId, string email, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Retires the user's unused reset tokens, issues a new one and mails the password reset link.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Whether the e-mail was handed to the provider.</returns>
    Task<bool> SendPasswordResetAsync(int userId, string email, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Mails an organization invitation link carrying <paramref name="token"/>.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="organizationName"></param>
    /// <param name="inviterName"></param>
    /// <param name="role"></param>
    /// <param name="token"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Whether the e-mail was handed to the provider.</returns>
    Task<bool> SendInvitationAsync(string email, string organizationName, string inviterName, OrgRole role, string token, CancellationToken cancellationToken);
}
