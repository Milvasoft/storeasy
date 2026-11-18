namespace Storeasy.Application.Dtos.SettingsDtos;

/// <summary>
/// What the web app must know about this instance before anyone signs in.
/// </summary>
public class PublicSettingsDto
{
    /// <summary>
    /// Who may register (<c>Storeasy:Registration:Mode</c>).
    /// </summary>
    public RegistrationMode RegistrationMode { get; set; }

    /// <summary>
    /// Whether "Sign in with Google" is configured.
    /// </summary>
    public bool GoogleSignInEnabled { get; set; }

    /// <summary>
    /// Version of the running API.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// VAPID public key the browsers subscribe to push with; null when web push is not configured.
    /// </summary>
    public string WebPushPublicKey { get; set; }
}
