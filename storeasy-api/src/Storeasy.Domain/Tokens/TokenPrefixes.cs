using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Tokens;

/// <summary>
/// Value prefixes of the bearer tokens storeasy issues (tools/CONTRACT.md 2). The prefix tells the authentication which
/// table to look in and lets log redaction recognize a token.
/// </summary>
public static class TokenPrefixes
{
    /// <summary>
    /// Personal access token.
    /// </summary>
    public const string PersonalAccessToken = "sea_";

    /// <summary>
    /// CI token.
    /// </summary>
    public const string CiToken = "sci_";

    /// <summary>
    /// Upload token.
    /// </summary>
    public const string UploadToken = "sut_";

    /// <summary>
    /// Kind of <paramref name="token"/> by its prefix; null for anything else (a JWT).
    /// </summary>
    public static TokenKind? KindOf(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        if (token.StartsWith(PersonalAccessToken, StringComparison.Ordinal))
            return TokenKind.PersonalAccessToken;

        if (token.StartsWith(CiToken, StringComparison.Ordinal))
            return TokenKind.CiToken;

        if (token.StartsWith(UploadToken, StringComparison.Ordinal))
            return TokenKind.UploadToken;

        return null;
    }

    /// <summary>
    /// The displayable prefix of a token value (the kind prefix and three characters).
    /// </summary>
    public static string DisplayPrefixOf(string token) => token == null ? null : token[..Math.Min(token.Length, 7)];
}
