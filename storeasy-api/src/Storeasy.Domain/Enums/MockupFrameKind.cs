using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// storeasy's own parametric device frames, drawn in code (no manufacturer artwork). A screenshot class names the frame
/// its images are drawn with; a template can turn the frame off. Values are explicit and never reused.
/// </summary>
public enum MockupFrameKind : byte
{
    /// <summary>
    /// No device: the screenshot (or only the caption, for a feature graphic) on the background.
    /// </summary>
    [Description("No frame")]
    None = 1,

    /// <summary>
    /// Phone body with a bezel and an island, notch, hole or no cutout.
    /// </summary>
    [Description("Phone")]
    Phone = 2,

    /// <summary>
    /// Tablet body with an even bezel and a camera dot.
    /// </summary>
    [Description("Tablet")]
    Tablet = 3,

    /// <summary>
    /// Foldable, folded: the narrow outer display with a hinge edge.
    /// </summary>
    [Description("Foldable outer display")]
    FoldableOuter = 4,

    /// <summary>
    /// Foldable, open: the wide inner display with a crease hint along the fold.
    /// </summary>
    [Description("Foldable inner display")]
    FoldableInner = 5,

    /// <summary>
    /// Desktop window with a title bar.
    /// </summary>
    [Description("Desktop window")]
    Desktop = 6,
}
