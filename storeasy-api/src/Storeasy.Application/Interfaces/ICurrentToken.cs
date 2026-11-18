namespace Storeasy.Application.Interfaces;

/// <summary>
/// The storeasy token of the current request (personal access token, CI token or upload token), read from the claims
/// the token authentication set. <see cref="Kind"/> is null for a web session. Registered scoped.
/// </summary>
public interface ICurrentToken
{
    /// <summary>
    /// Kind of the token; null when the request is a web session or anonymous.
    /// </summary>
    TokenKind? Kind { get; }

    /// <summary>
    /// Row id of the token.
    /// </summary>
    long? TokenId { get; }

    /// <summary>
    /// Organization the token is bound to (it wins over any organization id in the request).
    /// </summary>
    int? OrganizationId { get; }

    /// <summary>
    /// Whether a personal access token is read only.
    /// </summary>
    bool ReadOnly { get; }

    /// <summary>
    /// Whether a CI token covers every app.
    /// </summary>
    bool AllApps { get; }

    /// <summary>
    /// Apps a CI or upload token is limited to.
    /// </summary>
    IReadOnlyList<int> AppIds { get; }

    /// <summary>
    /// Display prefix of the token.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Name of the token.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Trusted publisher rule an upload token was exchanged with.
    /// </summary>
    int? TrustedPublisherId { get; }

    /// <summary>
    /// Whether the request is made with a CI token or an upload token (no person behind it).
    /// </summary>
    bool IsMachine => Kind is TokenKind.CiToken or TokenKind.UploadToken;

    /// <summary>
    /// Whether a CI or upload token may act on <paramref name="appId"/>; always true for other callers.
    /// </summary>
    bool AllowsApp(int appId) => !IsMachine || AllApps || AppIds.Contains(appId);
}
