namespace Storeasy.Application.Utils.Constants;

/// <summary>
/// Claims the token authentication puts on a request made with a personal access token, a CI token or an upload token
/// (read through <see cref="Interfaces.ICurrentToken"/>). A web session (JWT) carries none of them.
/// </summary>
public static class TokenClaimNames
{
    /// <summary>
    /// Authentication scheme of the storeasy tokens.
    /// </summary>
    public const string AuthenticationScheme = "StoreasyToken";

    /// <summary>
    /// Token kind (<see cref="Domain.Enums.TokenKind"/> as a number).
    /// </summary>
    public const string Kind = "tk";

    /// <summary>
    /// Token row id.
    /// </summary>
    public const string TokenId = "tid";

    /// <summary>
    /// Organization the token is bound to.
    /// </summary>
    public const string OrganizationId = "torg";

    /// <summary>
    /// "true" for a read only personal access token.
    /// </summary>
    public const string ReadOnly = "tro";

    /// <summary>
    /// "true" when a CI token covers every app.
    /// </summary>
    public const string AllApps = "tall";

    /// <summary>
    /// Comma separated app ids of a CI or upload token.
    /// </summary>
    public const string AppIds = "tapps";

    /// <summary>
    /// Display prefix of the token.
    /// </summary>
    public const string Prefix = "tpfx";

    /// <summary>
    /// Name of the token.
    /// </summary>
    public const string Name = "tname";

    /// <summary>
    /// Trusted publisher rule of an upload token.
    /// </summary>
    public const string TrustedPublisherId = "ttp";
}
