namespace Storeasy.Application.Dtos.AccountDtos;

/// <summary>
/// A started Google sign in: the browser goes to <see cref="AuthorizationUrl"/>; Google sends it back to the web app's
/// callback page with a code and the same <see cref="State"/>, which the page posts to <c>account/login/google</c>.
/// </summary>
public class GoogleSignInStartDto
{
    /// <summary>
    /// Google's authorization address with the client id, redirect address, scopes, state, nonce and PKCE challenge.
    /// </summary>
    public string AuthorizationUrl { get; set; }

    /// <summary>
    /// Opaque single use value that ties the callback to this start (valid for a few minutes).
    /// </summary>
    public string State { get; set; }
}
