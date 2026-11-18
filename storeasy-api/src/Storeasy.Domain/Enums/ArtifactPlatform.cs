using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Platform of a package. Values are explicit and never reused.
/// </summary>
public enum ArtifactPlatform : byte
{
    /// <summary>
    /// Apple platforms (IPA).
    /// </summary>
    [Description("iOS")]
    Ios = 1,

    /// <summary>
    /// Android (AAB, APK).
    /// </summary>
    [Description("Android")]
    Android = 2,

    /// <summary>
    /// Windows (MSIX, APPX).
    /// </summary>
    [Description("Windows")]
    Windows = 3,
}
