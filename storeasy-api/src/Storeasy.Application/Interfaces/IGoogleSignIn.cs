using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Google sign in with the OpenID Connect authorization code flow and PKCE. <see cref="StartAsync"/> keeps the code
/// verifier and the nonce on the server (Redis, keyed by the state, single use) and returns Google's authorization
/// address; <see cref="CompleteAsync"/> takes the state back, exchanges the code with the client secret and the verifier,
/// and validates the id token (signature from Google's keys, issuer, audience, lifetime, nonce).
/// </summary>
public interface IGoogleSignIn
{
    /// <summary>
    /// Whether sign in with Google is configured.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Starts a sign in. Null when the state cannot be stored (Redis unavailable).
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<GoogleSignInStartDto> StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Completes a sign in started by <see cref="StartAsync"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="state"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<GoogleSignInResult> CompleteAsync(string code, string state, CancellationToken cancellationToken);
}

/// <summary>
/// The person Google signed in.
/// </summary>
/// <param name="Subject">Google's stable account id (<c>sub</c>).</param>
/// <param name="Email">E-mail address.</param>
/// <param name="EmailVerified">Whether Google verified the address.</param>
/// <param name="GivenName">Given name, if shared.</param>
/// <param name="FamilyName">Family name, if shared.</param>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string GivenName, string FamilyName);

/// <summary>
/// Outcome of <see cref="IGoogleSignIn.CompleteAsync"/>: the identity, or the message key of the failure.
/// </summary>
/// <param name="Identity">The identity on success.</param>
/// <param name="ErrorKey">Message key on failure.</param>
public sealed record GoogleSignInResult(GoogleIdentity Identity, string ErrorKey)
{
    /// <summary>
    /// A failure.
    /// </summary>
    /// <param name="errorKey"></param>
    /// <returns></returns>
    public static GoogleSignInResult Fail(string errorKey) => new(null, errorKey);
}
