using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Package format. Values are explicit and never reused.
/// </summary>
public enum PackageKind : byte
{
    /// <summary>
    /// iOS app archive.
    /// </summary>
    [Description("IPA")]
    Ipa = 1,

    /// <summary>
    /// Android App Bundle.
    /// </summary>
    [Description("AAB")]
    Aab = 2,

    /// <summary>
    /// MSIX or APPX package or bundle.
    /// </summary>
    [Description("MSIX")]
    Msix = 3,

    /// <summary>
    /// Android package.
    /// </summary>
    [Description("APK")]
    Apk = 4,

    /// <summary>
    /// Browser extension ZIP (manifest.json at the root).
    /// </summary>
    [Description("Extension ZIP")]
    ExtensionZip = 5,

    /// <summary>
    /// Firefox extension package (.xpi: a ZIP with manifest.json at its root).
    /// </summary>
    [Description("XPI")]
    Xpi = 6,
}
