namespace Storeasy.Application.Interfaces;

/// <summary>
/// Checks the OIDC token Cloud Pub/Sub puts on a push request (<c>Authorization: Bearer &lt;jwt&gt;</c>): signed by Google,
/// not expired, issued for the receiver address (audience) and for the subscription's service account with a verified
/// e-mail. Registered as a singleton; tests replace its HTTP client.
/// </summary>
public interface IGooglePushTokenValidator
{
    /// <summary>
    /// Whether the <c>Authorization</c> header value carries a valid push token of <paramref name="serviceAccount"/> for
    /// <paramref name="audience"/>.
    /// </summary>
    /// <param name="authorization">The <c>Authorization</c> header value.</param>
    /// <param name="audience">Expected audience.</param>
    /// <param name="serviceAccount">Expected service account e-mail.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> ValidateAsync(string authorization, string audience, string serviceAccount, CancellationToken cancellationToken);
}
