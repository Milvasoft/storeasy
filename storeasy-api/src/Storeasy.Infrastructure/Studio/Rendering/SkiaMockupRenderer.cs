using SkiaSharp;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Studio;

namespace Storeasy.Infrastructure.Studio.Rendering;

/// <summary>
/// <see cref="IMockupRenderer"/> with SkiaSharp (MIT) and HarfBuzz shaping. Layers, back to front: background (solid,
/// gradient, or image covering the canvas), shapes and badges under the device, the device frame with the screenshot,
/// the caption block, layers marked above. The layout is computed at the class size and scaled for previews, so a preview
/// is the export in small. The canvas starts opaque white and the PNG is written without an alpha channel (App Store
/// Connect refuses transparency; the other stores accept opaque images).
/// </summary>
/// <param name="fonts"></param>
public sealed class SkiaMockupRenderer(StudioFontLibrary fonts) : IMockupRenderer
{
    private const float _aspectTolerance = 0.15f;

    private const float _captionOnlyTypeScale = 1.8f;

    private static readonly SKSamplingOptions _sampling = new(SKCubicResampler.Mitchell);

    private static readonly SKColor _screenColor = new(0x0B, 0x0B, 0x0F);

    private readonly StudioFontLibrary _fonts = fonts;

    /// <inheritdoc/>
    public MockupRenderResult Render(MockupRenderRequest request)
    {
        var spec = MockupSpecRules.Upgrade(request.Spec);
        var size = request.Size;
        var scale = Math.Clamp(request.OutputScale <= 0 ? 1 : request.OutputScale, 0.05f, 1f);
        var width = Math.Max(1, (int)Math.Round(size.Width * scale));
        var height = Math.Max(1, (int)Math.Round(size.Height * scale));
        var warnings = new List<MockupRenderIssue>();

        using var screenshot = Decode(request.Screenshot);

        if (request.Screenshot != null && screenshot == null)
            return Failed(width, height, warnings, new MockupRenderIssue(MockupWarningCodes.AssetUnreadable, "The raw screenshot could not be decoded."));

        var geometry = request.Frame == MockupFrameKind.None && screenshot == null
            ? null
            : DeviceFramePainter.Measure(request.Frame, spec.Frame, size, size.Aspect);

        if (geometry != null && screenshot != null)
        {
            var failure = CheckScreenshot(screenshot, geometry.Screen, warnings, request.FailWhenTooSmall);

            if (failure != null)
                return Failed(width, height, warnings, failure);
        }
        else if (geometry != null)
        {
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.ScreenshotMissing, "The slot has no raw screenshot; the screen is drawn empty."));
        }

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        using var typesetter = new CaptionTypesetter(_fonts);

        canvas.Clear(SKColors.White);
        canvas.Scale(width / (float)size.Width, height / (float)size.Height);

        DrawBackground(canvas, spec.Background, size, request.BackgroundImage, warnings);
        DrawLayers(canvas, spec, size, request.Locale, above: false, typesetter);

        if (geometry != null)
            DeviceFramePainter.Draw(canvas, geometry, spec.Frame, (c, screen) => DrawScreenshot(c, screen, screenshot));

        var captions = LayoutCaptions(typesetter, spec, size, request.Locale, request.Title, request.Subtitle, geometry == null, warnings);

        DrawCaptions(canvas, typesetter, spec, size, captions, geometry == null);
        DrawLayers(canvas, spec, size, request.Locale, above: true, typesetter);

        canvas.Flush();

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return new MockupRenderResult(data.ToArray(), width, height, warnings);
    }

    /// <inheritdoc/>
    public MockupCaptionMeasure MeasureCaptions(MockupSpec spec, PixelSize size, MockupFrameKind frame, string locale, string title, string subtitle)
    {
        spec = MockupSpecRules.Upgrade(spec);

        var warnings = new List<MockupRenderIssue>();

        using var typesetter = new CaptionTypesetter(_fonts);

        var captions = LayoutCaptions(typesetter, spec, size, locale, title, subtitle, frame == MockupFrameKind.None && size.IsLandscape, warnings);

        return new MockupCaptionMeasure(captions.Title.Lines.Count, spec.Text.Title.MaxLines, captions.Title.Size,
                                        captions.Subtitle.Lines.Count, spec.Text.Subtitle.MaxLines, captions.Subtitle.Size,
                                        warnings);
    }

    private static MockupRenderResult Failed(int width, int height, List<MockupRenderIssue> warnings, MockupRenderIssue failure) => new(null, width, height, warnings, failure);

    private static SKImage Decode(byte[] encoded) => encoded == null || encoded.Length == 0 ? null : SKImage.FromEncodedData(encoded)?.ToRasterImage(true);

    /// <summary>
    /// Upscale and aspect checks of the raw screenshot against the screen it fills (at class size), and the alpha check.
    /// </summary>
    private static MockupRenderIssue CheckScreenshot(SKImage screenshot, SKRect screen, List<MockupRenderIssue> warnings, bool failWhenTooSmall)
    {
        var cover = FitsByCover(screenshot, screen);
        var upscale = cover
            ? Math.Max(screen.Width / screenshot.Width, screen.Height / screenshot.Height)
            : Math.Min(screen.Width / screenshot.Width, screen.Height / screenshot.Height);

        if (!cover)
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.AspectMismatch, $"The raw screenshot ({screenshot.Width}x{screenshot.Height}) has another aspect than the screen; it is fitted inside."));

        if (upscale > MockupExportRules.MaxUpscale)
        {
            var issue = new MockupRenderIssue(MockupWarningCodes.AssetTooSmall, $"The raw screenshot ({screenshot.Width}x{screenshot.Height}) needs {upscale:P0} enlargement; the limit is {MockupExportRules.MaxUpscale:P0}.");

            if (failWhenTooSmall)
                return issue;

            warnings.Add(issue);
        }
        else if (upscale > MockupExportRules.WarnUpscale + 0.005f)
        {
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.AssetUpscaled, $"The raw screenshot is enlarged by {upscale:P0}; sharpness may drop."));
        }

        if (HasTransparency(screenshot))
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.AlphaFlattened, "The raw screenshot has transparent pixels; they are flattened onto the screen color."));

        return null;
    }

    private static bool FitsByCover(SKImage screenshot, SKRect screen)
        => Math.Abs((screenshot.Width / (float)screenshot.Height / (screen.Width / screen.Height)) - 1) <= _aspectTolerance;

    private static bool HasTransparency(SKImage image)
    {
        if (image.AlphaType == SKAlphaType.Opaque)
            return false;

        using var bitmap = SKBitmap.FromImage(image);
        using var converted = bitmap.ColorType == SKColorType.Rgba8888 ? null : new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul));

        var source = bitmap;

        if (converted != null && bitmap.CopyTo(converted, SKColorType.Rgba8888))
            source = converted;

        var pixels = source.GetPixelSpan();

        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 255)
                return true;
        }

        return false;
    }

    private static void DrawScreenshot(SKCanvas canvas, SKRect screen, SKImage screenshot)
    {
        using (var fill = new SKPaint { Color = _screenColor })
            canvas.DrawRect(screen, fill);

        if (screenshot == null)
            return;

        var cover = FitsByCover(screenshot, screen);
        var scale = cover
            ? Math.Max(screen.Width / screenshot.Width, screen.Height / screenshot.Height)
            : Math.Min(screen.Width / screenshot.Width, screen.Height / screenshot.Height);

        var drawWidth = screenshot.Width * scale;
        var drawHeight = screenshot.Height * scale;
        var target = SKRect.Create(screen.MidX - (drawWidth / 2), screen.MidY - (drawHeight / 2), drawWidth, drawHeight);

        using var paint = new SKPaint { IsAntialias = true };

        canvas.DrawImage(screenshot, target, _sampling, paint);
    }

    private static void DrawBackground(SKCanvas canvas, MockupBackgroundSpec background, PixelSize size, byte[] image, List<MockupRenderIssue> warnings)
    {
        var colors = background.Colors.Select(c => DeviceFramePainter.ParseColor(c, SKColors.White)).ToArray();
        var rect = SKRect.Create(0, 0, size.Width, size.Height);

        using var paint = new SKPaint { IsAntialias = true, Color = colors.Length > 0 ? colors[0] : SKColors.White };

        if (background.Kind == MockupSpecRules.BackgroundKinds.Gradient && colors.Length >= 2)
        {
            var radians = background.Angle * MathF.PI / 180f;
            var direction = new SKPoint(MathF.Cos(radians), MathF.Sin(radians));
            var half = ((Math.Abs(size.Width * direction.X) + Math.Abs(size.Height * direction.Y)) / 2) + 0.5f;
            var center = new SKPoint(size.Width / 2f, size.Height / 2f);

            paint.Shader = SKShader.CreateLinearGradient(new SKPoint(center.X - (direction.X * half), center.Y - (direction.Y * half)),
                                                         new SKPoint(center.X + (direction.X * half), center.Y + (direction.Y * half)),
                                                         colors,
                                                         SKShaderTileMode.Clamp);
        }

        canvas.DrawRect(rect, paint);

        paint.Shader?.Dispose();

        if (background.Kind != MockupSpecRules.BackgroundKinds.Image)
            return;

        using var decoded = Decode(image);

        if (decoded == null)
        {
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.BackgroundMissing, "The background image is missing or unreadable; the first color was used."));

            return;
        }

        var scale = Math.Max(size.Width / (float)decoded.Width, size.Height / (float)decoded.Height);
        var target = SKRect.Create((size.Width - (decoded.Width * scale)) / 2, (size.Height - (decoded.Height * scale)) / 2, decoded.Width * scale, decoded.Height * scale);

        using var imagePaint = new SKPaint { IsAntialias = true };

        canvas.DrawImage(decoded, target, _sampling, imagePaint);
    }

    /// <summary>
    /// Lays out the caption block. A caption only image (a feature graphic: no device) sets its type larger, since the
    /// caption is all there is.
    /// </summary>
    private static CaptionLayout LayoutCaptions(CaptionTypesetter typesetter, MockupSpec spec, PixelSize size, string locale, string title, string subtitle, bool captionOnly, List<MockupRenderIssue> warnings)
    {
        var text = spec.Text;
        var unit = (float)Math.Min(size.Width, size.Height);
        var margin = text.Margin * unit;

        if (captionOnly)
            unit *= _captionOnlyTypeScale;
        var maxWidth = size.Width - (2 * margin);
        var culture = MockupSpecRules.Culture(locale);

        if (text.Uppercase)
        {
            title = title?.ToUpper(culture);
            subtitle = subtitle?.ToUpper(culture);
        }

        var titleBlock = LayoutStyle(typesetter, text, text.Title, "title", title, locale, unit, maxWidth, warnings);
        var subtitleBlock = LayoutStyle(typesetter, text, text.Subtitle, "subtitle", subtitle, locale, unit, maxWidth, warnings);

        return new CaptionLayout(titleBlock, subtitleBlock, margin, text.Gap * unit);
    }

    private static TextBlock LayoutStyle(CaptionTypesetter typesetter, MockupTextSpec text, MockupTextStyleSpec style, string field, string content, string locale, float unit, float maxWidth, List<MockupRenderIssue> warnings)
    {
        if (string.IsNullOrWhiteSpace(content))
            return TextBlock.Empty;

        var nearest = StudioFontLibrary.NearestWeight(text.Font, style.Weight);

        if (nearest != style.Weight)
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.FontMissing, $"Weight {style.Weight} of {text.Font} is not embedded; {nearest} is used.", field));

        var block = typesetter.Layout(content, text.Font, style.Weight, locale, style.Size * unit, style.MinSize * unit, style.MaxLines, style.AutoFit, maxWidth, style.LineHeight);

        if (block.Lines.Count > style.MaxLines)
            warnings.Add(new MockupRenderIssue(MockupWarningCodes.CaptionOverflow, $"The {field} needs {block.Lines.Count} lines; the limit is {style.MaxLines}.", field, block.Lines.Count, style.MaxLines));

        return block;
    }

    private static void DrawCaptions(SKCanvas canvas, CaptionTypesetter typesetter, MockupSpec spec, PixelSize size, CaptionLayout captions, bool captionOnly)
    {
        var text = spec.Text;
        var gap = captions.Title.Lines.Count > 0 && captions.Subtitle.Lines.Count > 0 ? captions.Gap : 0;
        var blockHeight = captions.Title.Height + gap + captions.Subtitle.Height;

        float top;

        if (captionOnly)
            top = (size.Height - blockHeight) / 2;
        else if (text.Position == MockupSpecRules.Positions.Bottom)
            top = size.Height - captions.Margin - blockHeight;
        else
            top = captions.Margin;

        var left = captions.Margin;
        var right = size.Width - captions.Margin;
        var color = DeviceFramePainter.ParseColor(text.Color, SKColors.White);

        typesetter.Draw(canvas, captions.Title, left, right, top, text.Alignment, DeviceFramePainter.ParseColor(text.Title.Color, color));
        typesetter.Draw(canvas, captions.Subtitle, left, right, top + captions.Title.Height + gap, text.Alignment, DeviceFramePainter.ParseColor(text.Subtitle.Color, color));
    }

    private static void DrawLayers(SKCanvas canvas, MockupSpec spec, PixelSize size, string locale, bool above, CaptionTypesetter typesetter)
    {
        var unit = (float)Math.Min(size.Width, size.Height);

        foreach (var layer in spec.Layers.Where(l => l != null && l.Above == above))
        {
            var color = DeviceFramePainter.ParseColor(layer.Color, SKColors.White);

            using var paint = new SKPaint { IsAntialias = true, Color = color.WithAlpha((byte)Math.Round(color.Alpha * Math.Clamp(layer.Opacity, 0, 1))) };

            if (layer.Type == MockupSpecRules.Layers.Shape)
            {
                var rect = SKRect.Create(layer.X * size.Width, layer.Y * size.Height, layer.Width * size.Width, layer.Height * size.Height);

                if (layer.Shape == MockupSpecRules.Layers.Ellipse)
                    canvas.DrawOval(rect, paint);
                else
                    canvas.DrawRoundRect(rect, layer.Shape == MockupSpecRules.Layers.RoundRect ? layer.Radius * unit : 0, layer.Shape == MockupSpecRules.Layers.RoundRect ? layer.Radius * unit : 0, paint);

                continue;
            }

            var label = BadgeText(layer, locale);

            if (string.IsNullOrWhiteSpace(label))
                continue;

            var fontSize = layer.TextSize * unit;
            var block = typesetter.Layout(label, spec.Text.Font, 700, locale, fontSize, fontSize, 1, false, size.Width, 1.2f);
            var lineWidth = block.Lines.Count == 0 ? 0 : block.Lines.Max(l => l.Width);
            var pill = SKRect.Create((layer.X * size.Width) - (lineWidth / 2) - (fontSize * 0.7f), (layer.Y * size.Height) - (block.Height / 2) - (fontSize * 0.3f), lineWidth + (fontSize * 1.4f), block.Height + (fontSize * 0.6f));

            canvas.DrawRoundRect(pill, pill.Height / 2, pill.Height / 2, paint);
            typesetter.Draw(canvas, block, pill.Left, pill.Right, pill.Top + (fontSize * 0.3f), MockupSpecRules.Alignments.Center, DeviceFramePainter.ParseColor(layer.TextColor, SKColors.White));
        }
    }

    private static string BadgeText(MockupLayerSpec layer, string locale)
    {
        if (layer.Texts == null)
            return null;

        if (locale != null && layer.Texts.TryGetValue(locale, out var exact))
            return exact;

        var language = locale?.Split('-')[0];

        if (language != null && layer.Texts.TryGetValue(language, out var byLanguage))
            return byLanguage;

        return layer.Texts.TryGetValue(MockupSpecRules.Layers.AnyLocale, out var any) ? any : layer.Texts.Values.FirstOrDefault();
    }

    private sealed record CaptionLayout(TextBlock Title, TextBlock Subtitle, float Margin, float Gap);
}
