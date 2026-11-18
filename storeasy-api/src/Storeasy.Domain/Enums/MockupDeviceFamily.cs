using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Device family of a raw screenshot in a mockup slot. A class without a raw screenshot of its own family falls back to the
/// phone screenshot (see <see cref="Studio.MockupExportRules.PickSlotAsset"/>). Values are explicit and never reused.
/// </summary>
public enum MockupDeviceFamily : byte
{
    /// <summary>
    /// Phone screenshot (also used for foldable classes).
    /// </summary>
    [Description("Phone")]
    Phone = 1,

    /// <summary>
    /// Tablet screenshot.
    /// </summary>
    [Description("Tablet")]
    Tablet = 2,

    /// <summary>
    /// Desktop screenshot (window content).
    /// </summary>
    [Description("Desktop")]
    Desktop = 3,
}
