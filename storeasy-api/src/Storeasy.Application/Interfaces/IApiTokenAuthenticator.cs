namespace Storeasy.Application.Interfaces;

/// <summary>
/// Authenticates the storeasy bearer tokens (personal access, CI and upload tokens) by the SHA-256 of the presented value.
/// A personal access token works only while its owner is still a member of its organization; the last use is written at
/// most every few minutes. Registered scoped.
/// </summary>
public interface IApiTokenAuthenticator
{
    /// <summary>
    /// Looks the token up and checks its state.
    /// </summary>
    /// <param name="token">The presented value.</param>
    /// <param name="clientName">Client name from the User-Agent header, recorded as the last use.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ApiTokenAuthentication> AuthenticateAsync(string token, string clientName, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a token authentication. <see cref="FailureKey"/> is null on success.
/// </summary>
public sealed class ApiTokenAuthentication
{
    /// <summary>
    /// Message key of the failure (<see cref="MessageKey.Unauthorized"/>, <see cref="MessageKey.TokenExpired"/>,
    /// <see cref="MessageKey.CiTokenExpired"/>, <see cref="MessageKey.TokenRevoked"/>); null on success.
    /// </summary>
    public string FailureKey { get; init; }

    /// <summary>
    /// Token kind.
    /// </summary>
    public TokenKind Kind { get; init; }

    /// <summary>
    /// Token row id.
    /// </summary>
    public long TokenId { get; init; }

    /// <summary>
    /// Organization of the token.
    /// </summary>
    public int OrganizationId { get; init; }

    /// <summary>
    /// Owner of a personal access token.
    /// </summary>
    public int? UserId { get; init; }

    /// <summary>
    /// User name of the owner.
    /// </summary>
    public string UserName { get; init; }

    /// <summary>
    /// User type of the owner.
    /// </summary>
    public UserType? UserType { get; init; }

    /// <summary>
    /// Whether a personal access token is read only.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Whether a CI token covers every app.
    /// </summary>
    public bool AllApps { get; init; }

    /// <summary>
    /// Apps of a CI or upload token.
    /// </summary>
    public IReadOnlyList<int> AppIds { get; init; } = [];

    /// <summary>
    /// Display prefix.
    /// </summary>
    public string Prefix { get; init; }

    /// <summary>
    /// Name of the token.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Trusted publisher rule of an upload token.
    /// </summary>
    public int? TrustedPublisherId { get; init; }

    /// <summary>
    /// Whether the token was accepted.
    /// </summary>
    public bool Succeeded => FailureKey == null;

    /// <summary>
    /// A failed authentication.
    /// </summary>
    public static ApiTokenAuthentication Failure(string messageKey) => new() { FailureKey = messageKey };
}
