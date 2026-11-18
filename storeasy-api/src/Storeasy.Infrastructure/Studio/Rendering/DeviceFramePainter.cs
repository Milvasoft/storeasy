using SkiaSharp;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Studio;

namespace Storeasy.Infrastructure.Studio.Rendering;

/// <summary>
/// storeasy's own parametric device frames, drawn with paths: a rounded body in the template's color with an edge light,
/// an even bezel, and per kind an island, notch or camera hole (phone), a camera dot in the bezel (tablet), a hinge edge
/// (foldable outer display), a crease hint along the fold (foldable inner display) or a title bar with generic window
/// controls (desktop). No manufacturer artwork, logo or product shape is used: proportions come only from the screenshot
/// class's aspect ratio and the template's parameters.
/// </summary>
public static class DeviceFramePainter
{
    private const float _desktopTitleBar = 0.04f;

    /// <summary>
    /// Geometry of the device on a canvas of <paramref name="canvas"/> pixels before rotation: body width from the
    /// template's scale (of the canvas width, or of 1.25 times the height on a landscape canvas so a window keeps room for
    /// the caption), screen with the class's aspect, body around it, top at the template's offset.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="frame"></param>
    /// <param name="canvas"></param>
    /// <param name="screenAspect">Width divided by height of the screen (the class's aspect).</param>
    /// <returns></returns>
    public static FrameGeometry Measure(MockupFrameKind kind, MockupFrameSpec frame, PixelSize canvas, float screenAspect)
    {
        var (radius, bezel) = MockupFrameDefaults.Of(kind);

        radius *= (frame.CornerRadius ?? MockupFrameDefaults.PhoneRadius) / MockupFrameDefaults.PhoneRadius;
        bezel *= (frame.Bezel ?? MockupFrameDefaults.PhoneBezel) / MockupFrameDefaults.PhoneBezel;

        var reference = canvas.IsLandscape ? canvas.Height * 1.25f : canvas.Width;
        var bodyWidth = frame.Scale * reference;
        var bezelPx = bezel * bodyWidth;
        var titleBar = kind == MockupFrameKind.Desktop ? _desktopTitleBar * bodyWidth : 0;
        var screenWidth = bodyWidth - (2 * bezelPx);
        var screenHeight = screenWidth / screenAspect;
        var bodyHeight = screenHeight + (2 * bezelPx) + titleBar;

        var left = (canvas.Width / 2f) + (frame.OffsetX * canvas.Width) - (bodyWidth / 2);
        var top = frame.OffsetY * canvas.Height;

        var body = SKRect.Create(left, top, bodyWidth, bodyHeight);
        var screen = SKRect.Create(left + bezelPx, top + bezelPx + titleBar, screenWidth, screenHeight);
        var bodyRadius = radius * bodyWidth;
        var screenRadius = kind == MockupFrameKind.Desktop ? 0 : Math.Max(0, bodyRadius - bezelPx);

        return new FrameGeometry(kind, body, screen, bodyRadius, screenRadius, titleBar);
    }

    /// <summary>
    /// Draws the frame and lets <paramref name="drawScreen"/> fill the screen (the canvas is clipped to it).
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="geometry"></param>
    /// <param name="frame"></param>
    /// <param name="drawScreen"></param>
    public static void Draw(SKCanvas canvas, FrameGeometry geometry, MockupFrameSpec frame, Action<SKCanvas, SKRect> drawScreen)
    {
        var color = ParseColor(frame.Color, new SKColor(0x1C, 0x1C, 0x22));
        var body = geometry.Body;

        canvas.Save();
        canvas.RotateDegrees(frame.Rotation, body.MidX, body.MidY);

        if (frame.Shadow)
            DrawShadow(canvas, body, geometry.BodyRadius);

        if (geometry.Kind == MockupFrameKind.None)
        {
            DrawScreen(canvas, geometry.Screen, geometry.BodyRadius, drawScreen);
            canvas.Restore();

            return;
        }

        if (geometry.Kind is MockupFrameKind.Phone or MockupFrameKind.FoldableOuter)
            DrawSideButtons(canvas, body, Shade(color, -0.12f));

        using (var bodyPaint = new SKPaint { Color = color, IsAntialias = true })
            canvas.DrawRoundRect(body, geometry.BodyRadius, geometry.BodyRadius, bodyPaint);

        using (var edge = new SKPaint { Color = Shade(color, 0.22f), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, body.Width * 0.006f) })
        {
            var inset = edge.StrokeWidth / 2;

            canvas.DrawRoundRect(SKRect.Inflate(body, -inset, -inset), geometry.BodyRadius - inset, geometry.BodyRadius - inset, edge);
        }

        if (geometry.Kind == MockupFrameKind.Desktop)
            DrawTitleBar(canvas, body, geometry, color);

        DrawScreen(canvas, geometry.Screen, geometry.ScreenRadius, drawScreen);

        switch (geometry.Kind)
        {
            case MockupFrameKind.Phone:
                DrawCutout(canvas, geometry.Screen, frame.Cutout);
                break;
            case MockupFrameKind.FoldableOuter:
                DrawCutout(canvas, geometry.Screen, MockupSpecRules.Cutouts.Hole);
                DrawHinge(canvas, body, geometry.BodyRadius, color);
                break;
            case MockupFrameKind.FoldableInner:
                DrawCrease(canvas, geometry.Screen);
                DrawHole(canvas, geometry.Screen.Left + (geometry.Screen.Width * 0.75f), geometry.Screen.Top + (geometry.Screen.Width * 0.03f), geometry.Screen.Width * 0.014f);
                break;
            case MockupFrameKind.Tablet:
                using (var camera = new SKPaint { Color = Shade(color, 0.18f), IsAntialias = true })
                    canvas.DrawCircle(body.MidX, body.Top + ((geometry.Screen.Top - body.Top) / 2), body.Width * 0.006f, camera);
                break;
        }

        canvas.Restore();
    }

    /// <summary>
    /// Parses <c>#RRGGBB</c> or <c>#RRGGBBAA</c>; <paramref name="fallback"/> when it is not a color.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="fallback"></param>
    /// <returns></returns>
    public static SKColor ParseColor(string value, SKColor fallback)
    {
        if (!MockupSpecRules.IsColor(value))
            return fallback;

        var rgb = Convert.ToUInt32(value.Substring(1, 6), 16);
        var alpha = value.Length == 9 ? Convert.ToByte(value.Substring(7, 2), 16) : (byte)255;

        return new SKColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);
    }

    /// <summary>
    /// <paramref name="color"/> lightened (positive <paramref name="amount"/>) or darkened (negative) towards white or black.
    /// </summary>
    /// <param name="color"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static SKColor Shade(SKColor color, float amount)
    {
        var target = amount >= 0 ? 255f : 0f;
        var t = Math.Abs(amount);

        byte Mix(byte channel) => (byte)Math.Round(channel + ((target - channel) * t));

        return new SKColor(Mix(color.Red), Mix(color.Green), Mix(color.Blue), color.Alpha);
    }

    private static void DrawScreen(SKCanvas canvas, SKRect screen, float radius, Action<SKCanvas, SKRect> drawScreen)
    {
        canvas.Save();

        using (var clip = new SKRoundRect(screen, radius))
            canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);

        drawScreen(canvas, screen);

        canvas.Restore();
    }

    private static void DrawShadow(SKCanvas canvas, SKRect body, float radius)
    {
        using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, body.Width * 0.03f);
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 90), IsAntialias = true, MaskFilter = blur };

        var offset = body;

        offset.Offset(0, body.Width * 0.02f);

        canvas.DrawRoundRect(offset, radius, radius, shadow);
    }

    private static void DrawSideButtons(SKCanvas canvas, SKRect body, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        var width = body.Width * 0.014f;
        var radius = width / 2;

        canvas.DrawRoundRect(SKRect.Create(body.Left - (width * 0.6f), body.Top + (body.Height * 0.17f), width, body.Height * 0.06f), radius, radius, paint);
        canvas.DrawRoundRect(SKRect.Create(body.Left - (width * 0.6f), body.Top + (body.Height * 0.25f), width, body.Height * 0.06f), radius, radius, paint);
        canvas.DrawRoundRect(SKRect.Create(body.Right - (width * 0.4f), body.Top + (body.Height * 0.21f), width, body.Height * 0.1f), radius, radius, paint);
    }

    private static void DrawCutout(SKCanvas canvas, SKRect screen, string cutout)
    {
        var width = screen.Width;

        switch (cutout)
        {
            case MockupSpecRules.Cutouts.Island:
                using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
                {
                    var pill = SKRect.Create(screen.MidX - (width * 0.15f), screen.Top + (width * 0.028f), width * 0.30f, width * 0.088f);

                    canvas.DrawRoundRect(pill, pill.Height / 2, pill.Height / 2, paint);
                }

                break;
            case MockupSpecRules.Cutouts.Notch:
                using (var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true })
                {
                    var depth = width * 0.072f;
                    var notch = SKRect.Create(screen.MidX - (width * 0.24f), screen.Top - depth, width * 0.48f, depth * 2);

                    canvas.Save();
                    canvas.ClipRect(screen);
                    canvas.DrawRoundRect(notch, depth * 0.55f, depth * 0.55f, paint);
                    canvas.Restore();
                }

                break;
            case MockupSpecRules.Cutouts.Hole:
                DrawHole(canvas, screen.MidX, screen.Top + (width * 0.04f), width * 0.024f);
                break;
        }
    }

    private static void DrawHole(SKCanvas canvas, float x, float y, float radius)
    {
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var lens = new SKPaint { Color = new SKColor(0x22, 0x26, 0x33), IsAntialias = true };

        canvas.DrawCircle(x, y, radius, paint);
        canvas.DrawCircle(x, y, radius * 0.45f, lens);
    }

    private static void DrawHinge(SKCanvas canvas, SKRect body, float radius, SKColor color)
    {
        var hinge = SKRect.Create(body.Left, body.Top, body.Width * 0.035f, body.Height);

        using var shader = SKShader.CreateLinearGradient(new SKPoint(hinge.Left, 0), new SKPoint(hinge.Right, 0), [Shade(color, -0.25f), Shade(color, 0.12f), color], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsAntialias = true };

        canvas.Save();

        using (var clip = new SKRoundRect(body, radius))
            canvas.ClipRoundRect(clip, SKClipOperation.Intersect, true);

        canvas.DrawRect(hinge, paint);
        canvas.Restore();
    }

    private static void DrawCrease(SKCanvas canvas, SKRect screen)
    {
        var width = screen.Width * 0.022f;
        var crease = SKRect.Create(screen.MidX - (width / 2), screen.Top, width, screen.Height);

        SKColor[] colors = [new(0, 0, 0, 0), new(0, 0, 0, 46), new(255, 255, 255, 30), new(0, 0, 0, 46), new(0, 0, 0, 0)];

        using var shader = SKShader.CreateLinearGradient(new SKPoint(crease.Left, 0), new SKPoint(crease.Right, 0), colors, SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader };

        canvas.DrawRect(crease, paint);
    }

    private static void DrawTitleBar(SKCanvas canvas, SKRect body, FrameGeometry geometry, SKColor color)
    {
        var bar = SKRect.Create(body.Left, body.Top, body.Width, geometry.TitleBarHeight);
        var glyph = Shade(color, color.Red + color.Green + color.Blue > 382 ? -0.6f : 0.65f);
        var size = geometry.TitleBarHeight * 0.26f;
        var y = bar.MidY;

        using var paint = new SKPaint { Color = glyph, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, size * 0.16f) };

        // Close, maximize and minimize from the right edge: generic line glyphs.
        var x = bar.Right - (geometry.TitleBarHeight * 0.9f);

        canvas.DrawLine(x - size, y - size, x + size, y + size, paint);
        canvas.DrawLine(x - size, y + size, x + size, y - size, paint);

        x -= geometry.TitleBarHeight * 1.3f;
        canvas.DrawRect(SKRect.Create(x - size, y - size, size * 2, size * 2), paint);

        x -= geometry.TitleBarHeight * 1.3f;
        canvas.DrawLine(x - size, y, x + size, y, paint);
    }
}

/// <summary>
/// Placement of a device on a canvas, before rotation.
/// </summary>
/// <param name="Kind">Frame kind.</param>
/// <param name="Body">Body rectangle.</param>
/// <param name="Screen">Screen rectangle.</param>
/// <param name="BodyRadius">Body corner radius in pixels.</param>
/// <param name="ScreenRadius">Screen corner radius in pixels.</param>
/// <param name="TitleBarHeight">Height of a desktop title bar, 0 otherwise.</param>
public sealed record FrameGeometry(MockupFrameKind Kind, SKRect Body, SKRect Screen, float BodyRadius, float ScreenRadius, float TitleBarHeight);
