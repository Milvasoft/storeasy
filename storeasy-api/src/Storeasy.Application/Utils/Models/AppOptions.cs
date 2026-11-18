namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Public facing settings of the web app (configuration section <see cref="SectionName"/>), bound once and registered as
/// a singleton. Links in e-mails point at <see cref="BaseUrl"/>.
/// </summary>
public class AppOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:App";

    /// <summary>
    /// Path of the web app page that verifies an e-mail address (<c>?token=</c> is appended).
    /// </summary>
    public const string VerifyEmailPath = "verify-email";

    /// <summary>
    /// Path of the web app page that sets a new password (<c>?token=</c> is appended).
    /// </summary>
    public const string ResetPasswordPath = "reset-password";

    /// <summary>
    /// Path of the web app page that accepts an organization invitation (<c>?token=</c> is appended).
    /// </summary>
    public const string InvitationPath = "invitation";

    /// <summary>
    /// Public address of the web app, e.g. <c>https://storeasy.example.com</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Builds a web app link to <paramref name="path"/> carrying <paramref name="token"/> as the <c>token</c> query value.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public string BuildTokenLink(string path, string token) => $"{(BaseUrl ?? string.Empty).TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token ?? string.Empty)}";
}
