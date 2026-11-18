using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a Microsoft Store submission belongs to.
/// </summary>
public enum MsSubmissionTarget : byte
{
    /// <summary>
    /// An MSIX app submission.
    /// </summary>
    [Description("App")]
    App = 1,

    /// <summary>
    /// A package flight submission.
    /// </summary>
    [Description("Package flight")]
    Flight = 2,

    /// <summary>
    /// An add-on (in-app product) submission.
    /// </summary>
    [Description("Add-on")]
    Addon = 3,

    /// <summary>
    /// An MSI/EXE product submission.
    /// </summary>
    [Description("MSI/EXE product")]
    Installer = 4,
}
