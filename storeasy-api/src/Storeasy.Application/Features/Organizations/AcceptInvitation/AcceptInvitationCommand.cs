using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Organizations.AcceptInvitation;

/// <summary>
/// Accepts an invitation with the token from the link. The signed in user's e-mail must match the invited e-mail.
/// Returns the organization id.
/// </summary>
public record AcceptInvitationCommand : ICommand<int>
{
    /// <summary>
    /// Token from the invitation link.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }
}
