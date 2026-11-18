namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Google sign in settings (configuration section <see cref="SectionName"/>), bound once and registered as a singleton.
/// Sign in with Google is off until both <see cref="ClientId"/> and <see cref="ClientSecret"/> are set (an OAuth client of
/// type "Web application" whose authorized redirect URI is <c>{Storeasy:App:BaseUrl}/auth/google/callback</c>).
/// </summary>
public class GoogleAuthOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Auth:Google";

    /// <summary>
    /// Google's issuer.
    /// </summary>
    public const string DefaultAuthority = "https://accounts.google.com";

    /// <summary>
    /// OAuth client id.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// OAuth client secret (secret, set through the environment).
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// OpenID Connect issuer whose discovery document is read. Google's unless a test points it elsewhere; https only.
    /// </summary>
    public string Authority { get; set; } = DefaultAuthority;

    /// <summary>
    /// How long a started sign in may take before its state is forgotten.
    /// </summary>
    public int StateLifetimeMinutes { get; set; } = 10;

    /// <summary>
    /// Whether sign in with Google is configured.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
