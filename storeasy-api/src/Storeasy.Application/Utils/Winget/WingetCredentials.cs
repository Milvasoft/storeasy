using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// The secret of a winget connection: a GitHub token and the owner's fork of winget-pkgs (<c>owner/name</c>). Stored
/// encrypted like every store secret; the fork is also in the fingerprint.
/// </summary>
public sealed class WingetCredentials
{
    /// <summary>
    /// GitHub personal access token: classic (<c>ghp_</c>, <c>public_repo</c> scope) or fine-grained (<c>github_pat_</c>).
    /// </summary>
    [JsonPropertyName("token")]
    public string Token { get; set; }

    /// <summary>
    /// The fork of winget-pkgs the branches are pushed to, as <c>owner/name</c>.
    /// </summary>
    [JsonPropertyName("fork")]
    public string Fork { get; set; }

    /// <summary>
    /// Owner part of <see cref="Fork"/>.
    /// </summary>
    [JsonIgnore]
    public string ForkOwner => Fork?.Split('/')[0];

    /// <summary>
    /// Name part of <see cref="Fork"/>.
    /// </summary>
    [JsonIgnore]
    public string ForkName => Fork?.Split('/') is { Length: 2 } parts ? parts[1] : null;
}

/// <summary>
/// Parses and checks a winget secret offline.
/// </summary>
public static partial class WingetCredentialParser
{
    /// <summary>
    /// Token kind of a classic personal access token.
    /// </summary>
    public const string ClassicToken = "classic";

    /// <summary>
    /// Token kind of a fine-grained personal access token.
    /// </summary>
    public const string FineGrainedToken = "fine-grained";

    /// <summary>
    /// Longest token accepted.
    /// </summary>
    public const int TokenMaxLength = 255;

    [GeneratedRegex("^(ghp_[A-Za-z0-9]{30,250}|github_pat_[A-Za-z0-9_]{30,250})$")]
    private static partial Regex TokenRegex();

    // GitHub user and organization names: letters, digits and single hyphens, up to 39; repository names: letters,
    // digits, '.', '-', '_', up to 100.
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}/[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex ForkRegex();

    /// <summary>
    /// The credentials of <paramref name="json"/>, or null with <paramref name="errorKey"/> set. Accepts
    /// <c>{"token": "...", "fork": "owner/winget-pkgs"}</c>; a fork given as a GitHub address is reduced to
    /// <c>owner/name</c>.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="errorKey"></param>
    /// <returns></returns>
    public static WingetCredentials Parse(string json, out string errorKey)
    {
        errorKey = MessageKey.WingetSecretInvalid;

        WingetCredentials credentials;

        try
        {
            credentials = JsonSerializer.Deserialize<WingetCredentials>(json ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }

        if (credentials == null || string.IsNullOrWhiteSpace(credentials.Token) || string.IsNullOrWhiteSpace(credentials.Fork))
            return null;

        credentials.Token = credentials.Token.Trim();
        credentials.Fork = NormalizeFork(credentials.Fork);

        if (credentials.Token.Length > TokenMaxLength || !TokenRegex().IsMatch(credentials.Token))
        {
            errorKey = MessageKey.WingetTokenInvalid;
            return null;
        }

        if (credentials.Fork == null || !ForkRegex().IsMatch(credentials.Fork))
        {
            errorKey = MessageKey.WingetForkInvalid;
            return null;
        }

        errorKey = null;

        return credentials;
    }

    /// <summary>
    /// <c>classic</c> or <c>fine-grained</c>, from the token's prefix.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public static string KindOf(string token) => token?.StartsWith("github_pat_", StringComparison.Ordinal) == true ? FineGrainedToken : ClassicToken;

    private static string NormalizeFork(string fork)
    {
        var value = fork.Trim().TrimEnd('/');

        foreach (var prefix in new[] { "https://github.com/", "http://github.com/", "github.com/" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                value = value[prefix.Length..];
        }

        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];

        return value.Split('/').Length == 2 ? value : null;
    }
}
