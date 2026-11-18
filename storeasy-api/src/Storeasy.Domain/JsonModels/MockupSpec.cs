namespace Storeasy.Domain.JsonModels;

/// <summary>
/// The design of a mockup template, stored as jsonb and versioned by <see cref="SchemaVersion"/>. It is the single source
/// of the preview and of the export (docs/urun-plani.md K8): the server renders both from it. Every length is a fraction,
/// so one design renders at every catalog size: horizontal values of the canvas width, vertical values of the canvas
/// height, type and frame sizes of the shorter canvas side (the "unit"). Text content is not here: titles and subtitles
/// live per locale and slot in <see cref="Studio.MockupCaption"/>. See <see cref="Studio.MockupSpecRules"/> for the
/// allowed values and the defaults, and storeasy-api/docs/studio.md for the documented schema.
/// </summary>
public class MockupSpec
{
    /// <summary>
    /// Schema version of this document. Readers upgrade older versions (<see cref="Studio.MockupSpecRules.Upgrade"/>).
    /// </summary>
    public int SchemaVersion { get; set; } = Studio.MockupSpecRules.CurrentSchemaVersion;

    /// <summary>
    /// Design canvas.
    /// </summary>
    public MockupCanvasSpec Canvas { get; set; }

    /// <summary>
    /// Background.
    /// </summary>
    public MockupBackgroundSpec Background { get; set; }

    /// <summary>
    /// Device frame and the placement of the screenshot.
    /// </summary>
    public MockupFrameSpec Frame { get; set; }

    /// <summary>
    /// Caption styling.
    /// </summary>
    public MockupTextSpec Text { get; set; }

    /// <summary>
    /// Badges and shapes, drawn in order.
    /// </summary>
    public List<MockupLayerSpec> Layers { get; set; }
}

/// <summary>
/// The canvas a template is designed on. Exports render to every selected class; this is the class the editor opens with.
/// </summary>
public class MockupCanvasSpec
{
    /// <summary>
    /// Screenshot class key (a rules catalog id, e.g. <c>apple.screenshots.iphone-dynamic-island-medium</c>).
    /// </summary>
    public string ClassKey { get; set; }
}

/// <summary>
/// Background of a mockup.
/// </summary>
public class MockupBackgroundSpec
{
    /// <summary>
    /// <c>solid</c>, <c>gradient</c> or <c>image</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Colors as <c>#RRGGBB</c>: one for solid, two to five for a gradient (the first is also the fill under an image).
    /// </summary>
    public List<string> Colors { get; set; }

    /// <summary>
    /// Gradient angle in degrees (0 = left to right, 90 = top to bottom).
    /// </summary>
    public float Angle { get; set; }

    /// <summary>
    /// Image asset of the organization for <c>image</c>, drawn to cover the canvas.
    /// </summary>
    public long? AssetId { get; set; }
}

/// <summary>
/// Device frame parameters and screenshot placement. The frame drawn depends on the class (phone, tablet, foldable, desktop
/// window) unless <see cref="Kind"/> forces one; radius and bezel are fractions of the body width and scale the other
/// kinds' defaults by the same ratio.
/// </summary>
public class MockupFrameSpec
{
    /// <summary>
    /// <c>auto</c> (by class), <c>none</c> (screenshot without a device), or a fixed <c>phone</c>, <c>tablet</c>,
    /// <c>foldable</c>, <c>desktop</c>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Phone cutout: <c>island</c>, <c>notch</c>, <c>hole</c> or <c>none</c>.
    /// </summary>
    public string Cutout { get; set; }

    /// <summary>
    /// Body color as <c>#RRGGBB</c>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Corner radius as a fraction of the body width; null for the frame's default.
    /// </summary>
    public float? CornerRadius { get; set; }

    /// <summary>
    /// Bezel thickness as a fraction of the body width; null for the frame's default.
    /// </summary>
    public float? Bezel { get; set; }

    /// <summary>
    /// Body width as a fraction of the canvas width.
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    /// Horizontal shift of the device center as a fraction of the canvas width (0 = centered).
    /// </summary>
    public float OffsetX { get; set; }

    /// <summary>
    /// Top of the device as a fraction of the canvas height. Negative or large values let the device bleed off the canvas.
    /// </summary>
    public float OffsetY { get; set; }

    /// <summary>
    /// Rotation in degrees around the device center (clockwise).
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Soft shadow under the device.
    /// </summary>
    public bool Shadow { get; set; } = true;
}

/// <summary>
/// Caption block: where it sits and how the title and subtitle look.
/// </summary>
public class MockupTextSpec
{
    /// <summary>
    /// <c>inter</c> or <c>noto-sans</c>. Scripts the family lacks (Arabic, Hebrew, Thai, Devanagari, CJK) fall back to the
    /// embedded Noto Sans fonts of that script.
    /// </summary>
    public string Font { get; set; }

    /// <summary>
    /// Default text color as <c>#RRGGBB</c>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// <c>top</c> or <c>bottom</c>.
    /// </summary>
    public string Position { get; set; }

    /// <summary>
    /// <c>center</c>, <c>start</c> or <c>end</c> (start is right for right to left text).
    /// </summary>
    public string Alignment { get; set; }

    /// <summary>
    /// Distance of the block from the canvas edges, as a fraction of the unit.
    /// </summary>
    public float Margin { get; set; }

    /// <summary>
    /// Space between title and subtitle, as a fraction of the unit.
    /// </summary>
    public float Gap { get; set; }

    /// <summary>
    /// Upper case the captions with the rules of their locale (Turkish dotted and dotless i included).
    /// </summary>
    public bool Uppercase { get; set; }

    /// <summary>
    /// Title style.
    /// </summary>
    public MockupTextStyleSpec Title { get; set; }

    /// <summary>
    /// Subtitle style.
    /// </summary>
    public MockupTextStyleSpec Subtitle { get; set; }
}

/// <summary>
/// Style of one caption line group.
/// </summary>
public class MockupTextStyleSpec
{
    /// <summary>
    /// Font size as a fraction of the unit.
    /// </summary>
    public float Size { get; set; }

    /// <summary>
    /// Font weight, 100 to 900. A weight the embedded fonts lack is drawn with the nearest one (with a warning).
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Most lines before the text counts as overflowing.
    /// </summary>
    public int MaxLines { get; set; }

    /// <summary>
    /// Shrink the size (down to <see cref="MinSize"/>) until the text fits in <see cref="MaxLines"/>.
    /// </summary>
    public bool AutoFit { get; set; }

    /// <summary>
    /// Smallest size auto fit may use, as a fraction of the unit.
    /// </summary>
    public float MinSize { get; set; }

    /// <summary>
    /// Line height as a multiple of the font size.
    /// </summary>
    public float LineHeight { get; set; }

    /// <summary>
    /// Color as <c>#RRGGBB</c>; null for the block's color.
    /// </summary>
    public string Color { get; set; }
}

/// <summary>
/// A badge (a pill with a short localized text) or a shape (rectangle, rounded rectangle, ellipse).
/// </summary>
public class MockupLayerSpec
{
    /// <summary>
    /// <c>badge</c> or <c>shape</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// For shapes: <c>rect</c>, <c>round-rect</c> or <c>ellipse</c>.
    /// </summary>
    public string Shape { get; set; }

    /// <summary>
    /// Left edge (shape) or center (badge) as a fraction of the canvas width.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Top edge (shape) or center (badge) as a fraction of the canvas height.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Shape width as a fraction of the canvas width.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Shape height as a fraction of the canvas height.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Corner radius of a rounded rectangle as a fraction of the unit.
    /// </summary>
    public float Radius { get; set; }

    /// <summary>
    /// Fill color as <c>#RRGGBB</c>.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Opacity, 0 to 1.
    /// </summary>
    public float Opacity { get; set; } = 1;

    /// <summary>
    /// Draw over the device and the captions instead of under the device.
    /// </summary>
    public bool Above { get; set; }

    /// <summary>
    /// Badge text by locale; the <c>*</c> entry is used for locales without their own.
    /// </summary>
    public Dictionary<string, string> Texts { get; set; }

    /// <summary>
    /// Badge font size as a fraction of the unit.
    /// </summary>
    public float TextSize { get; set; }

    /// <summary>
    /// Badge text color as <c>#RRGGBB</c>.
    /// </summary>
    public string TextColor { get; set; }
}
