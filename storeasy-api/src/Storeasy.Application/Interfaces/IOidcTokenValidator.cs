namespace Storeasy.Application.Interfaces;

/// <summary>
/// Validates a CI provider's OIDC token: the issuer must be allow-listed (GitHub, Azure DevOps, gitlab.com, or one of the
/// extra issuers given to <see cref="ValidateAsync"/>), the signature must verify with a key of the issuer's
/// JWKS (fetched over https from the issuer's discovery document and cached), and <c>exp</c>, <c>nbf</c> and <c>iat</c>
/// must hold with a 5 minute skew. The audience and the claims are checked by the caller against its rules. The token is
/// never stored or logged.
/// </summary>
public interface IOidcTokenValidator
{
    /// <summary>
    /// Validates <paramref name="token"/>.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="extraIssuers">Further allowed issuers (self-managed GitLab URLs of the app's rules).</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OidcValidation> ValidateAsync(string token, IReadOnlyCollection<string> extraIssuers, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of an OIDC token validation.
/// </summary>
/// <param name="Valid">Whether the token passed.</param>
/// <param name="Issuer">Issuer of the token.</param>
/// <param name="Audiences">Audiences of the token.</param>
/// <param name="Claims">String claims of the token (arrays joined by commas).</param>
/// <param name="Error">Why it failed (safe to show: no claim value, no token).</param>
public sealed record OidcValidation(bool Valid, string Issuer, IReadOnlyList<string> Audiences, IReadOnlyDictionary<string, string> Claims, string Error)
{
    /// <summary>
    /// A failed validation.
    /// </summary>
    public static OidcValidation Fail(string error, string issuer = null) => new(false, issuer, [], new Dictionary<string, string>(), error);
}
