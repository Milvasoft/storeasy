using System.Text.Json.Serialization;

namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Bootstrap settings (configuration section <see cref="SectionName"/>, environment <c>Storeasy__Bootstrap__InitKey</c>),
/// bound once and registered as a singleton. Only the anonymous <c>developer/database/init</c> endpoint reads them.
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
}
