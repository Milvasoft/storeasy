using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// The OIDC exchange of trusted publishing (tools/CONTRACT.md 3.1, design 13.4-13.7): validates a CI provider's OIDC token,
/// matches it against the app's trusted publisher rules and issues a 15 minute upload token. The token is never stored;
/// the claims of the last attempt are kept on the rule for diagnosis.
/// </summary>
public interface IOidcExchangeService
{
    /// <summary>
    /// Exchanges <paramref name="token"/> for an upload token of <paramref name="appId"/>.
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="token"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<OidcExchangeDto>> ExchangeAsync(int appId, string token, CancellationToken cancellationToken);

    /// <summary>
    /// Checks <paramref name="token"/> against one rule without issuing anything (dry run).
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="ruleId"></param>
    /// <param name="token"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<TrustedPublisherTestDto>> TestAsync(int organizationId, int ruleId, string token, CancellationToken cancellationToken);
}
