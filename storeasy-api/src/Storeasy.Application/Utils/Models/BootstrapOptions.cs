using System.Text.Json.Serialization;

namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Bootstrap settings (configuration section <see cref="SectionName"/>, environment <c>Storeasy__Bootstrap__InitKey</c>),
/// bound once and registered as a singleton. The anonymous <c>developer/database/init</c> endpoint reads the init key, the
/// first run setup of a single team instance the setup code.
/// </summary>
public class BootstrapOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Bootstrap";

    /// <summary>
    /// Request header that carries <see cref="InitKey"/>. It is never logged.
    /// </summary>
    public const string InitKeyHeaderName = "X-Storeasy-Init-Key";

    /// <summary>
    /// Shortest <see cref="InitKey"/> that enables database initialization.
    /// </summary>
    public const int MinimumInitKeyLength = 32;

    /// <summary>
    /// One-off secret that <c>developer/database/init</c> requires in <see cref="InitKeyHeaderName"/>. Empty (the default)
    /// or shorter than <see cref="MinimumInitKeyLength"/> keeps the endpoint closed. Set it only on the temporary
    /// <c>MILVA_ENV=dev</c> container that initializes a database, never keep it.
    /// </summary>
    [JsonIgnore]
    public string InitKey { get; set; }

    /// <summary>
    /// True when <see cref="InitKey"/> is long enough to enable database initialization.
    /// </summary>
    [JsonIgnore]
    public bool IsInitKeyConfigured => !string.IsNullOrEmpty(InitKey) && InitKey.Length >= MinimumInitKeyLength;

    /// <summary>
    /// Shortest <see cref="SetupCode"/> that is accepted; a shorter value is ignored and a random code is generated.
    /// </summary>
    public const int MinimumSetupCodeLength = 12;

    /// <summary>
    /// Optional fixed first run setup code for automated installs (<c>Storeasy__Bootstrap__SetupCode</c>). Empty (the
    /// default): while the instance has no user, the API generates a random one time code at startup and writes it to
    /// the log once. Only read in <see cref="DeploymentMode.SingleTeam"/>.
    /// </summary>
    [JsonIgnore]
    public string SetupCode { get; set; }

    /// <summary>
    /// True when <see cref="SetupCode"/> is long enough to replace the generated code.
    /// </summary>
    [JsonIgnore]
    public bool IsSetupCodeConfigured => !string.IsNullOrWhiteSpace(SetupCode) && SetupCode.Trim().Length >= MinimumSetupCodeLength;
}
