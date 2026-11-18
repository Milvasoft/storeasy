using Storeasy.Domain.JsonModels;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Storeasy.Domain.Studio;

/// <summary>
/// Pure rules of the mockup template schema (<see cref="MockupSpec"/>): the allowed values, the defaults, validation and
/// upgrades between schema versions. Validation returns readable problems with their JSON path; the API answers them as
/// one 400.
/// </summary>
public static partial class MockupSpecRules
{
    /// <summary>
    /// Schema version this build writes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Most layers (badges and shapes) in one template.
    /// </summary>
    public const int MaxLayers = 20;

    /// <summary>
    /// Longest badge text.
    /// </summary>
    public const int MaxBadgeTextLength = 40;

    /// <summary>
    /// Background kinds.
    /// </summary>
    public static class BackgroundKinds
    {
        /// <summary>One color.</summary>
        public const string Solid = "solid";

        /// <summary>Linear gradient.</summary>
        public const string Gradient = "gradient";

        /// <summary>Image asset covering the canvas.</summary>
        public const string Image = "image";

        /// <summary>All kinds.</summary>
        public static readonly string[] All = [Solid, Gradient, Image];
    }

    /// <summary>
    /// Frame kinds a template may ask for.
    /// </summary>
    public static class FrameKinds
    {
        /// <summary>The class decides (phone, tablet, foldable, desktop window).</summary>
        public const string Auto = "auto";

        /// <summary>No device.</summary>
        public const string None = "none";

        /// <summary>Always a phone.</summary>
        public const string Phone = "phone";

        /// <summary>Always a tablet.</summary>
        public const string Tablet = "tablet";

        /// <summary>Always a foldable (outer or inner display by the class's aspect).</summary>
        public const string Foldable = "foldable";

        /// <summary>Always a desktop window.</summary>
        public const string Desktop = "desktop";

        /// <summary>All kinds.</summary>
        public static readonly string[] All = [Auto, None, Phone, Tablet, Foldable, Desktop];
    }

    /// <summary>
    /// Phone cutouts.
    /// </summary>
    public static class Cutouts
    {
        /// <summary>A floating pill at the top of the screen.</summary>
        public const string Island = "island";

        /// <summary>A notch attached to the top edge.</summary>
        public const string Notch = "notch";

        /// <summary>A single camera hole.</summary>
        public const string Hole = "hole";

        /// <summary>No cutout.</summary>
        public const string None = "none";

        /// <summary>All cutouts.</summary>
        public static readonly string[] All = [Island, Notch, Hole, None];
    }

    /// <summary>
    /// Embedded font families (SIL OFL 1.1).
    /// </summary>
    public static class Fonts
    {
        /// <summary>Inter.</summary>
        public const string Inter = "inter";

        /// <summary>Noto Sans.</summary>
        public const string NotoSans = "noto-sans";

        /// <summary>All families a template may choose.</summary>
        public static readonly string[] All = [Inter, NotoSans];
    }

    /// <summary>
    /// Caption positions.
    /// </summary>
    public static class Positions
    {
        /// <summary>Above the device.</summary>
        public const string Top = "top";

        /// <summary>Below the device.</summary>
        public const string Bottom = "bottom";

        /// <summary>All positions.</summary>
        public static readonly string[] All = [Top, Bottom];
    }

    /// <summary>
    /// Caption alignments.
    /// </summary>
    public static class Alignments
    {
        /// <summary>Centered.</summary>
        public const string Center = "center";

        /// <summary>Start edge (left, or right for right to left text).</summary>
        public const string Start = "start";

        /// <summary>End edge.</summary>
        public const string End = "end";

        /// <summary>All alignments.</summary>
        public static readonly string[] All = [Center, Start, End];
    }

    /// <summary>
    /// Layer types and shapes.
    /// </summary>
    public static class Layers
    {
        /// <summary>A pill with a localized text.</summary>
        public const string Badge = "badge";

        /// <summary>A plain shape.</summary>
        public const string Shape = "shape";

        /// <summary>Rectangle.</summary>
        public const string Rect = "rect";

        /// <summary>Rounded rectangle.</summary>
        public const string RoundRect = "round-rect";

        /// <summary>Ellipse.</summary>
        public const string Ellipse = "ellipse";

        /// <summary>Layer types.</summary>
        public static readonly string[] Types = [Badge, Shape];

        /// <summary>Shapes.</summary>
        public static readonly string[] Shapes = [Rect, RoundRect, Ellipse];

        /// <summary>Key of the badge text used for locales without their own.</summary>
        public const string AnyLocale = "*";
    }

    /// <summary>
    /// The default design: storeasy's ultramarine gradient, a phone with an island, Inter captions on top.
    /// </summary>
    /// <returns></returns>
    public static MockupSpec CreateDefault() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Canvas = new MockupCanvasSpec { ClassKey = StudioClassMap.DefaultClassKey },
        Background = new MockupBackgroundSpec { Kind = BackgroundKinds.Gradient, Colors = ["#4636FF", "#1B1464"], Angle = 90 },
        Frame = new MockupFrameSpec { Kind = FrameKinds.Auto, Cutout = Cutouts.Island, Color = "#1C1C22", Scale = 0.78f, OffsetX = 0, OffsetY = 0.27f, Rotation = 0, Shadow = true },
        Text = new MockupTextSpec
        {
            Font = Fonts.Inter,
            Color = "#FFFFFF",
            Position = Positions.Top,
            Alignment = Alignments.Center,
            Margin = 0.08f,
            Gap = 0.025f,
            Uppercase = false,
            Title = new MockupTextStyleSpec { Size = 0.085f, Weight = 700, MaxLines = 2, AutoFit = true, MinSize = 0.055f, LineHeight = 1.15f },
            Subtitle = new MockupTextStyleSpec { Size = 0.048f, Weight = 400, MaxLines = 2, AutoFit = true, MinSize = 0.036f, LineHeight = 1.25f },
        },
        Layers = [],
    };

    /// <summary>
    /// Upgrades <paramref name="spec"/> to <see cref="CurrentSchemaVersion"/> and fills missing parts with the defaults, so
    /// renderers never see a null section. A newer version than this build knows is returned as is (validation refuses it).
    /// </summary>
    /// <param name="spec"></param>
    /// <returns></returns>
    public static MockupSpec Upgrade(MockupSpec spec)
    {
        var defaults = CreateDefault();

        if (spec == null)
            return defaults;

        // Version 0 (no version written) has the same shape as version 1.
        if (spec.SchemaVersion <= 0)
            spec.SchemaVersion = CurrentSchemaVersion;

        spec.Canvas ??= defaults.Canvas;
        spec.Canvas.ClassKey ??= defaults.Canvas.ClassKey;
        spec.Background ??= defaults.Background;
        spec.Background.Kind ??= BackgroundKinds.Solid;
        spec.Background.Colors ??= [.. defaults.Background.Colors];
        spec.Frame ??= defaults.Frame;
        spec.Frame.Kind ??= FrameKinds.Auto;
        spec.Frame.Cutout ??= Cutouts.Island;
        spec.Frame.Color ??= defaults.Frame.Color;
        spec.Text ??= defaults.Text;
        spec.Text.Font ??= Fonts.Inter;
        spec.Text.Color ??= defaults.Text.Color;
        spec.Text.Position ??= Positions.Top;
        spec.Text.Alignment ??= Alignments.Center;
        spec.Text.Title ??= defaults.Text.Title;
        spec.Text.Subtitle ??= defaults.Text.Subtitle;
        spec.Layers ??= [];

        return spec;
    }

    /// <summary>
    /// Problems of <paramref name="spec"/> (empty when valid), each as <c>path: problem</c>.
    /// </summary>
    /// <param name="spec"></param>
    /// <returns></returns>
    public static List<string> Validate(MockupSpec spec)
    {
        var errors = new List<string>();

        if (spec == null)
        {
            errors.Add("spec: required");

            return errors;
        }

        if (spec.SchemaVersion != CurrentSchemaVersion)
            errors.Add($"schemaVersion: must be {CurrentSchemaVersion}");

        if (spec.Canvas?.ClassKey != null && !StudioClassMap.IsKnown(spec.Canvas.ClassKey))
            errors.Add("canvas.classKey: unknown screenshot class");

        ValidateBackground(spec.Background, errors);
        ValidateFrame(spec.Frame, errors);
        ValidateText(spec.Text, errors);
        ValidateLayers(spec.Layers, errors);

        return errors;
    }

    /// <summary>
    /// Whether <paramref name="color"/> is <c>#RRGGBB</c> or <c>#RRGGBBAA</c>.
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public static bool IsColor(string color) => color != null && ColorPattern().IsMatch(color);

    /// <summary>
    /// Whether <paramref name="locale"/> is a language tag as the stores use them (<c>tr</c>, <c>en-US</c>, <c>zh-Hans</c>).
    /// </summary>
    /// <param name="locale"></param>
    /// <returns></returns>
    public static bool IsLocale(string locale) => locale != null && locale.Length <= MockupTemplate.LocaleMaxLength && LocalePattern().IsMatch(locale);

    /// <summary>
    /// The locale's casing culture for <see cref="MockupTextSpec.Uppercase"/>; invariant when the tag is not a culture.
    /// </summary>
    /// <param name="locale"></param>
    /// <returns></returns>
    public static CultureInfo Culture(string locale)
    {
        try
        {
            return string.IsNullOrWhiteSpace(locale) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static void ValidateBackground(MockupBackgroundSpec background, List<string> errors)
    {
        if (background == null)
        {
            errors.Add("background: required");

            return;
        }

        if (!BackgroundKinds.All.Contains(background.Kind))
            errors.Add($"background.kind: one of {string.Join(", ", BackgroundKinds.All)}");

        var colors = background.Colors ?? [];
        var (min, max) = background.Kind == BackgroundKinds.Gradient ? (2, 5) : (1, 5);

        if (colors.Count < min || colors.Count > max)
            errors.Add($"background.colors: {min} to {max} colors");

        for (var i = 0; i < colors.Count; i++)
        {
            if (!IsColor(colors[i]))
                errors.Add($"background.colors[{i}]: #RRGGBB");
        }

        if (background.Angle is < 0 or > 360)
            errors.Add("background.angle: 0 to 360");

        if (background.Kind == BackgroundKinds.Image && background.AssetId is null or <= 0)
            errors.Add("background.assetId: required for an image background");
    }

    private static void ValidateFrame(MockupFrameSpec frame, List<string> errors)
    {
        if (frame == null)
        {
            errors.Add("frame: required");

            return;
        }

        if (!FrameKinds.All.Contains(frame.Kind))
            errors.Add($"frame.kind: one of {string.Join(", ", FrameKinds.All)}");

        if (!Cutouts.All.Contains(frame.Cutout))
            errors.Add($"frame.cutout: one of {string.Join(", ", Cutouts.All)}");

        if (!IsColor(frame.Color))
            errors.Add("frame.color: #RRGGBB");

        if (frame.CornerRadius is < 0 or > 0.3f)
            errors.Add("frame.cornerRadius: 0 to 0.3");

        if (frame.Bezel is < 0 or > 0.15f)
            errors.Add("frame.bezel: 0 to 0.15");

        if (frame.Scale is < 0.2f or > 1.5f)
            errors.Add("frame.scale: 0.2 to 1.5");

        if (frame.OffsetX is < -1 or > 1)
            errors.Add("frame.offsetX: -1 to 1");

        if (frame.OffsetY is < -1 or > 1)
            errors.Add("frame.offsetY: -1 to 1");

        if (frame.Rotation is < -45 or > 45)
            errors.Add("frame.rotation: -45 to 45");
    }

    private static void ValidateText(MockupTextSpec text, List<string> errors)
    {
        if (text == null)
        {
            errors.Add("text: required");

            return;
        }

        if (!Fonts.All.Contains(text.Font))
            errors.Add($"text.font: one of {string.Join(", ", Fonts.All)}");

        if (!IsColor(text.Color))
            errors.Add("text.color: #RRGGBB");

        if (!Positions.All.Contains(text.Position))
            errors.Add($"text.position: one of {string.Join(", ", Positions.All)}");

        if (!Alignments.All.Contains(text.Alignment))
            errors.Add($"text.alignment: one of {string.Join(", ", Alignments.All)}");

        if (text.Margin is < 0 or > 0.3f)
            errors.Add("text.margin: 0 to 0.3");

        if (text.Gap is < 0 or > 0.2f)
            errors.Add("text.gap: 0 to 0.2");

        ValidateStyle("text.title", text.Title, errors);
        ValidateStyle("text.subtitle", text.Subtitle, errors);
    }

    private static void ValidateStyle(string path, MockupTextStyleSpec style, List<string> errors)
    {
        if (style == null)
        {
            errors.Add($"{path}: required");

            return;
        }

        if (style.Size is < 0.01f or > 0.25f)
            errors.Add($"{path}.size: 0.01 to 0.25");

        if (style.Weight is < 100 or > 900 || style.Weight % 100 != 0)
            errors.Add($"{path}.weight: 100 to 900 in steps of 100");

        if (style.MaxLines is < 1 or > 6)
            errors.Add($"{path}.maxLines: 1 to 6");

        if (style.AutoFit && (style.MinSize < 0.01f || style.MinSize > style.Size))
            errors.Add($"{path}.minSize: 0.01 to size");

        if (style.LineHeight is < 0.8f or > 2.5f)
            errors.Add($"{path}.lineHeight: 0.8 to 2.5");

        if (style.Color != null && !IsColor(style.Color))
            errors.Add($"{path}.color: #RRGGBB");
    }

    private static void ValidateLayers(List<MockupLayerSpec> layers, List<string> errors)
    {
        if (layers == null)
            return;

        if (layers.Count > MaxLayers)
            errors.Add($"layers: at most {MaxLayers}");

        for (var i = 0; i < Math.Min(layers.Count, MaxLayers); i++)
        {
            var layer = layers[i];
            var path = $"layers[{i}]";

            if (layer == null)
            {
                errors.Add($"{path}: required");
                continue;
            }

            if (!Layers.Types.Contains(layer.Type))
                errors.Add($"{path}.type: one of {string.Join(", ", Layers.Types)}");

            if (layer.X is < -0.5f or > 1.5f || layer.Y is < -0.5f or > 1.5f)
                errors.Add($"{path}.x/y: -0.5 to 1.5");

            if (!IsColor(layer.Color))
                errors.Add($"{path}.color: #RRGGBB");

            if (layer.Opacity is < 0 or > 1)
                errors.Add($"{path}.opacity: 0 to 1");

            if (layer.Type == Layers.Shape)
            {
                if (!Layers.Shapes.Contains(layer.Shape))
                    errors.Add($"{path}.shape: one of {string.Join(", ", Layers.Shapes)}");

                if (layer.Width is <= 0 or > 2 || layer.Height is <= 0 or > 2)
                    errors.Add($"{path}.width/height: above 0, at most 2");

                if (layer.Radius is < 0 or > 0.5f)
                    errors.Add($"{path}.radius: 0 to 0.5");
            }

            if (layer.Type == Layers.Badge)
            {
                if (layer.Texts == null || layer.Texts.Count == 0 || layer.Texts.Values.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > MaxBadgeTextLength))
                    errors.Add($"{path}.texts: at least one text, each 1 to {MaxBadgeTextLength} characters");

                if (layer.TextSize is < 0.01f or > 0.2f)
                    errors.Add($"{path}.textSize: 0.01 to 0.2");

                if (!IsColor(layer.TextColor))
                    errors.Add($"{path}.textColor: #RRGGBB");
            }
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$")]
    private static partial Regex ColorPattern();

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LocalePattern();
}
