namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the winget channel (configuration section <see cref="SectionName"/>), bound once and registered as a
/// singleton. The defaults are GitHub and the community repository; tests point <see cref="ApiBaseUrl"/> at a fake.
/// </summary>
public class WingetOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Winget";

    /// <summary>
    /// GitHub REST API base address.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>
    /// The repository pull requests go to, as <c>owner/name</c>.
    /// </summary>
    public string Upstream { get; set; } = "microsoft/winget-pkgs";

    /// <summary>
    /// Default branch of <see cref="Upstream"/>.
    /// </summary>
    public string UpstreamBranch { get; set; } = "master";

    /// <summary>
    /// Largest installer downloaded to compute its hash (bytes).
    /// </summary>
    public long InstallerMaxBytes { get; set; } = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// Whether installer downloads must resolve to public addresses (off only in tests).
    /// </summary>
    public bool BlockPrivateDownloadAddresses { get; set; } = true;

    /// <summary>
    /// Most version folders whose pull requests a sync looks up by search.
    /// </summary>
    public int SearchLimit { get; set; } = 30;
}
