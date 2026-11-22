using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One state a Chrome Web Store revision was seen in, with the first time storeasy saw it.
/// </summary>
public class ChromeStateChange
{
    /// <summary>
    /// <c>published</c> or <c>submitted</c>.
    /// </summary>
    public string Revision { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ChromeItemState State { get; set; }

    /// <summary>
    /// Package version of the revision, when known.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// UTC instant storeasy first saw the state.
    /// </summary>
    public DateTime Date { get; set; }
}
