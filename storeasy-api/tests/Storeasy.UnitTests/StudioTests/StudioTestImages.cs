using FluentAssertions;
using SkiaSharp;
using Storeasy.Infrastructure.Studio;
using Storeasy.Infrastructure.Studio.Rendering;

namespace Storeasy.UnitTests.StudioTests;

/// <summary>
/// Shared fixtures of the studio tests: the renderer with the shipped fonts, synthetic raw screenshots, PNG inspection and
/// the golden image comparison.
/// </summary>
public static class StudioTestImages
{
    /// <summary>
    /// Set to 1 to rewrite the golden images from the current renderer (then review the diff of the PNGs).
    /// </summary>
    public const string UpdateGoldensVariable = "STOREASY_UPDATE_GOLDENS";

    private static readonly Lazy<StudioFontLibrary> _fonts = new(() => new StudioFontLibrary(StudioFontLibrary.ResolveFolder(null)));

    /// <summary>
    /// The renderer over the fonts copied next to the test binaries.
    /// </summary>
    public static SkiaMockupRenderer Renderer { get; } = new(_fonts.Value);

    /// <summary>
    /// Fonts copied next to the test binaries.
    /// </summary>
    public static StudioFontLibrary Fonts => _fonts.Value;

    /// <summary>
    /// The rules catalog of the repository.
    /// </summary>
    public static Domain.Studio.ScreenshotSizeCatalog Catalog { get; } = RulesCatalogScreenshotSizeSource.Build(Infrastructure.Services.RulesCatalog.Load(null).Current);

    /// <summary>
    /// A deterministic fake app screenshot: a status bar, a header, cards and a tab bar.
    /// </summary>
    public static byte[] Screenshot(int width, int height, byte alpha = 255)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = false };

        canvas.Clear(new SKColor(0xF4, 0xF5, 0xFA, alpha));

        paint.Color = new SKColor(0x46, 0x36, 0xFF, alpha);
        canvas.DrawRect(0, 0, width, height * 0.16f, paint);

        for (var i = 0; i < 5; i++)
        {
            paint.Color = new SKColor((byte)(0x30 + (i * 30)), 0xB0, (byte)(0xE0 - (i * 25)), alpha);
            canvas.DrawRect(width * 0.06f, height * (0.2f + (i * 0.13f)), width * 0.88f, height * 0.1f, paint);
        }

        paint.Color = new SKColor(0x22, 0x22, 0x2A, alpha);
        canvas.DrawRect(0, height * 0.92f, width, height * 0.08f, paint);

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>
    /// PNG color type from the IHDR chunk (2 = RGB, 6 = RGBA).
    /// </summary>
    public static int PngColorType(byte[] png) => png[25];

    /// <summary>
    /// Pixel size of a PNG from the IHDR chunk.
    /// </summary>
    public static (int Width, int Height) PngSize(byte[] png)
        => ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    /// <summary>
    /// Compares <paramref name="png"/> with the golden image <paramref name="name"/> with a tolerance for antialiasing and
    /// font rasterization differences between platforms: at most 2% of the pixels may differ by more than 48 in a channel,
    /// and the mean channel difference stays below 4. With <see cref="UpdateGoldensVariable"/> set, the golden is written.
    /// </summary>
    public static void ShouldMatchGolden(byte[] png, string name)
    {
        var path = Path.Combine(GoldenFolder(), name + ".png");

        if (Environment.GetEnvironmentVariable(UpdateGoldensVariable) == "1" || !File.Exists(path) && Environment.GetEnvironmentVariable("CI") == null)
        {
            File.WriteAllBytes(path, png);

            return;
        }

        File.Exists(path).Should().BeTrue($"the golden image {name}.png exists (run with {UpdateGoldensVariable}=1 to create it)");

        using var actual = SKBitmap.Decode(png);
        using var expected = SKBitmap.Decode(File.ReadAllBytes(path));

        actual.Width.Should().Be(expected.Width);
        actual.Height.Should().Be(expected.Height);

        long different = 0;
        double total = 0;

        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                var diff = Math.Max(Math.Abs(a.Red - e.Red), Math.Max(Math.Abs(a.Green - e.Green), Math.Abs(a.Blue - e.Blue)));

                total += (Math.Abs(a.Red - e.Red) + Math.Abs(a.Green - e.Green) + Math.Abs(a.Blue - e.Blue)) / 3.0;

                if (diff > 48)
                    different++;
            }
        }

        var pixels = (double)actual.Width * actual.Height;

        (different / pixels).Should().BeLessThan(0.02, $"{name}: at most 2% of the pixels may differ noticeably");
        (total / pixels).Should().BeLessThan(4, $"{name}: the mean difference stays small");
    }

    private static string GoldenFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Storeasy.UnitTests", "StudioTests", "Goldens");

            if (Directory.Exists(candidate))
                return candidate;
        }

        var local = Path.Combine(AppContext.BaseDirectory, "StudioTests", "Goldens");

        Directory.CreateDirectory(local);

        return local;
    }
}
