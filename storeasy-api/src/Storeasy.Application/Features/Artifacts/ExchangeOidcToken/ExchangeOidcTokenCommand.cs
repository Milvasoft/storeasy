using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.ExchangeOidcToken;

/// <summary>
/// Exchanges a CI provider's OIDC token for a 15 minute upload token of one app (tools/CONTRACT.md 3.1). Anonymous: the
/// OIDC token is the credential.
/// </summary>
public record ExchangeOidcTokenCommand : ICommand<OidcExchangeDto>
{
    /// <summary>
    /// App the package is for.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// The CI provider's OIDC token (JWT). Never stored or logged.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }

    /// <summary>
    /// Provider hint of the client; the server decides from the token's issuer.
    /// </summary>
    public OidcProvider? Provider { get; set; }
}
