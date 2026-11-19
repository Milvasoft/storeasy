using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Removes anything that looks like a credential from text before it is logged, stored or returned: bearer tokens,
/// JWTs, PEM private keys, Google access tokens, Apple webhook signatures, signed URL query strings and
/// <c>access_token</c> query values (the SignalR websocket token).
/// Ported from store-ops <c>redact()</c>.
/// </summary>
public static partial class SecretRedactor
{
    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]+")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PemRegex();

    [GeneratedRegex(@"ya29\.[A-Za-z0-9._-]+")]
    private static partial Regex GoogleTokenRegex();

    [GeneratedRegex(@"hmacsha256=[0-9a-fA-F]+")]
    private static partial Regex AppleSignatureRegex();

    [GeneratedRegex(@"access_token=[^&\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex AccessTokenQueryRegex();

    [GeneratedRegex(@"(https://[^\s""'?]+)\?[^\s""']*(?:Signature|signature|X-Amz|sig=|token=|access_token=)[^\s""']*")]
    private static partial Regex SignedUrlRegex();

    [GeneratedRegex(@"\b(?<kind>sea|sci|sut|whsec)_[A-Za-z0-9_-]{8,}")]
    private static partial Regex StoreasyTokenRegex();

    // Anthropic API keys (sk-ant-api03-..., sk-ant-admin01-...).
    [GeneratedRegex(@"\bsk-ant-[A-Za-z0-9_-]{8,}")]
    private static partial Regex AnthropicKeyRegex();

    /// <summary>
    /// Returns <paramref name="text"/> with credentials replaced by markers. Null stays null.
    /// </summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var output = BearerRegex().Replace(text, "Bearer [redacted]");
        output = JwtRegex().Replace(output, "[redacted-jwt]");
        output = PemRegex().Replace(output, "[redacted-private-key]");
        output = GoogleTokenRegex().Replace(output, "[redacted-token]");
        output = AppleSignatureRegex().Replace(output, "hmacsha256=[redacted]");
        output = SignedUrlRegex().Replace(output, "$1?[redacted-query]");
        output = AccessTokenQueryRegex().Replace(output, "access_token=[redacted]");
        output = StoreasyTokenRegex().Replace(output, match => match.Groups["kind"].Value + "_[redacted]");
        output = AnthropicKeyRegex().Replace(output, "sk-ant-[redacted]");

        return output;
    }

    /// <summary>
    /// Redacts and cuts <paramref name="text"/> to <paramref name="maxLength"/> characters.
    /// </summary>
    public static string RedactAndTrim(string text, int maxLength)
    {
        var output = Redact(text);

        if (output != null && output.Length > maxLength)
        {
            output = output[..maxLength];
        }

        return output;
    }
}
