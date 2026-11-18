namespace Storeasy.Application.Interfaces;

/// <summary>
/// Issues the one time account tokens and prepares the account e-mails (verification, password reset, invitation) in the
/// current request culture. Nothing is sent on the request path: the mails go into the e-mail outbox
/// (<see cref="IEmailOutbox"/>) and a background sender delivers them, so the answer time of an anonymous endpoint does
/// not depend on the provider or on whether an account exists. A failure to queue is logged and reported as false, never
/// thrown, so the calling flow (registration, invitation) completes and the user can ask for the mail again.
/// </summary>
public interface IAccountMailer
{
    /// <summary>
    /// Retires the user's unused verification tokens, issues a new one and queues the verification link.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="email"></param>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Whether the e-mail was queued.</returns>
    Task<bool> SendEmailVerificationAsync(int userId, string email, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a password reset request for <paramref name="email"/>, whether or not an account uses it. The account is
    /// looked up (and the token issued) by the background sender (<see cref="CreatePasswordResetAsync"/>), so the request
    /// does the same work for every address.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Called by the background sender for a queued password reset request: when an account uses <paramref name="email"/>,
    /// retires its unused reset tokens, issues a new one and returns the reset e-mail; null when no account uses it.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<EmailMessage> CreatePasswordResetAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Queues an organization invitation e-mail.
    /// </summary>
    /// <param name="mail"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Whether the e-mail was queued.</returns>
    Task<bool> SendInvitationAsync(InvitationMail mail, CancellationToken cancellationToken);
}

/// <summary>
/// What an invitation e-mail says.
/// </summary>
/// <param name="Email">Invited address.</param>
/// <param name="OrganizationName">Organization name.</param>
/// <param name="InviterName">Who invited.</param>
/// <param name="Role">Role on acceptance.</param>
/// <param name="CustomRoleName">Name of the custom role when <paramref name="Role"/> is <see cref="OrgRole.Custom"/>.</param>
/// <param name="Token">Invitation token (put into the link).</param>
public sealed record InvitationMail(string Email, string OrganizationName, string InviterName, OrgRole Role, string CustomRoleName, string Token);
