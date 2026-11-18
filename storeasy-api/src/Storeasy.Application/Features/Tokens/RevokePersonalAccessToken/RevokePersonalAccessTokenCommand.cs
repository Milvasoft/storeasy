using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Tokens.RevokePersonalAccessToken;

/// <summary>
/// Revokes one of the caller's personal access tokens (design 16.14). Effective on the next request made with it.
/// </summary>
public record RevokePersonalAccessTokenCommand : ICommand<int>
{
    /// <summary>
    /// Token id.
    /// </summary>
    public int TokenId { get; set; }
}
