using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One state a Firefox add-on or one of its versions was seen in, with the first time storeasy saw it.
/// </summary>
public class FirefoxStateChange
{
    /// <summary>
    /// <c>addon</c> (the add-on status) or <c>version</c> (the file status of a version).
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// The state, as its enum name (<see cref="FirefoxAddonStatus"/> or <see cref="FirefoxFileStatus"/>).
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Version, for a version state.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Channel of the version.
    /// </summary>
    public FirefoxVersionChannel Channel { get; set; }

    /// <summary>
    /// UTC instant storeasy first saw the state.
    /// </summary>
    public DateTime Date { get; set; }
}
