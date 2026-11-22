using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a package upload to a Chrome Web Store item (<c>uploadState</c>, <c>lastAsyncUploadState</c>). Values are
/// explicit and never reused.
/// </summary>
public enum ChromeUploadState : byte
{
    /// <summary>
    /// No upload in the last 24 hours, or a state the API did not name.
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// The package was accepted.
    /// </summary>
    [Description("Succeeded")]
    Succeeded = 1,

    /// <summary>
    /// The store is still processing the package; the status is read again.
    /// </summary>
    [Description("In progress")]
    InProgress = 2,

    /// <summary>
    /// The store refused the package.
    /// </summary>
    [Description("Failed")]
    Failed = 3,

    /// <summary>
    /// The store has no such upload.
    /// </summary>
    [Description("Not found")]
    Missing = 4,
}
