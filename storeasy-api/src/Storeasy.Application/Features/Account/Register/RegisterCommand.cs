using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.Register;

/// <summary>
/// Creates an account that signs in with its e-mail address (the user name is the lower case e-mail). Allowed by
/// <c>Storeasy:Registration:Mode</c>: Open (anyone), InviteOnly (a valid invitation for the same e-mail is required),
/// Closed (nobody). An invitation token, when given, is accepted in the same step. A verification e-mail is sent; the
/// account can sign in right away but cannot create organizations or store connections until the e-mail is verified.
/// Returns the new user id.
/// </summary>
public record RegisterCommand : ICommand<int>
{
    /// <summary>
    /// E-mail address; it is also the sign in name.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Password.
    /// </summary>
    [LogMasked]
    public string Password { get; set; }

    /// <summary>
    /// First name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Last name (optional).
    /// </summary>
    public string Surname { get; set; }

    /// <summary>
    /// Organization invitation token from the invitation link. Required in InviteOnly mode, optional otherwise.
    /// </summary>
    [LogMasked]
    public string InvitationToken { get; set; }
}
