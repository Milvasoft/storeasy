namespace Storeasy.Application.Interfaces;

/// <summary>
/// Access tokens revoked one by one (a signed out device), kept by SHA-256 hash until the token would expire anyway. The
/// session stamp revokes every token of a user; this list revokes single ones without signing the user out elsewhere.
/// Backed by Redis: while it is unavailable a revoked token keeps working until it expires (its session, and so its
/// refresh, is gone already).
/// </summary>
public interface ITokenRevocationList
{
    /// <summary>
    /// Revokes <paramref name="accessTokens"/>.
    /// </summary>
    /// <param name="accessTokens"></param>
    /// <returns></returns>
    Task RevokeAsync(IReadOnlyCollection<string> accessTokens);

    /// <summary>
    /// Whether <paramref name="accessToken"/> was revoked.
    /// </summary>
    /// <param name="accessToken"></param>
    /// <returns></returns>
    Task<bool> IsRevokedAsync(string accessToken);
}
