using System.Buffers.Text;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Format checks of the keys a browser subscription carries (Push API <c>PushSubscription.toJSON()</c>).
/// </summary>
public static class WebPushKeys
{
    /// <summary>
    /// Whether <paramref name="value"/> is a base64url P-256 public key (65 byte uncompressed point).
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsPublicKey(string value) => TryDecode(value, out var bytes) && bytes.Length == 65 && bytes[0] == 0x04;

    /// <summary>
    /// Whether <paramref name="value"/> is a base64url 16 byte authentication secret.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsAuthSecret(string value) => TryDecode(value, out var bytes) && bytes.Length == 16;

    /// <summary>
    /// Whether <paramref name="endpoint"/> is an absolute https address without user information.
    /// </summary>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    public static bool IsEndpoint(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && !uri.IsLoopback;

    /// <summary>
    /// Decodes base64url (padding tolerated); false when malformed.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static bool TryDecode(string value, out byte[] bytes)
    {
        bytes = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            bytes = Base64Url.DecodeFromChars(value.Trim().TrimEnd('='));

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
