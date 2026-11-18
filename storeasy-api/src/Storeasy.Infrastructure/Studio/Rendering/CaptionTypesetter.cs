using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Storeasy.Domain.Studio;
using System.Text;

namespace Storeasy.Infrastructure.Studio.Rendering;

/// <summary>
/// Lays out and draws caption text: splits it into runs by the first font of the fallback chain that has each character,
/// shapes every run with HarfBuzz (ligatures, Arabic joining, Indic clusters), breaks lines with
/// <see cref="CaptionLineBreaker"/> and shrinks the size for auto fit. Right to left paragraphs (Arabic, Hebrew) draw their
/// runs from right to left and align to the right by default (basic bidi: each run is shaped in its own direction, runs of
/// a line are reversed). One instance per render; it owns its shapers.
/// </summary>
/// <param name="fonts"></param>
public sealed class CaptionTypesetter(StudioFontLibrary fonts) : IDisposable
{
    private const float _autoFitStep = 0.95f;

    private readonly StudioFontLibrary _fonts = fonts;
    private readonly Dictionary<SKTypeface, SKShaper> _shapers = [];

    /// <summary>
    /// Lays out <paramref name="text"/> in <paramref name="maxWidth"/> pixels: at <paramref name="size"/>, or shrinking in
    /// steps down to <paramref name="minSize"/> until it fits in <paramref name="maxLines"/> when <paramref name="autoFit"/>.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="family"></param>
    /// <param name="weight"></param>
    /// <param name="locale"></param>
    /// <param name="size"></param>
    /// <param name="minSize"></param>
    /// <param name="maxLines"></param>
    /// <param name="autoFit"></param>
    /// <param name="maxWidth"></param>
    /// <param name="lineHeight">Line height as a multiple of the size.</param>
    /// <returns></returns>
    public TextBlock Layout(string text, string family, int weight, string locale, float size, float minSize, int maxLines, bool autoFit, float maxWidth, float lineHeight)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TextBlock.Empty;

        var chain = StudioFontLibrary.FallbackChain(family, locale).Select(f => _fonts.Get(f, weight)).ToList();
        var rightToLeft = IsRightToLeft(text);
        var current = size;

        while (true)
        {
            var lines = CaptionLineBreaker.Break(text, maxWidth, s => Measure(s, chain, current));
            var fits = lines.Count <= maxLines;

            if (fits || !autoFit || current * _autoFitStep < minSize)
            {
                var laidOut = lines.Select(line => LayoutLine(line, chain, current, rightToLeft)).ToList();

                return new TextBlock(laidOut, current, current * lineHeight, rightToLeft, Metrics(chain[0], current));
            }

            current *= _autoFitStep;
        }
    }

    /// <summary>
    /// Draws <paramref name="block"/> with its first line's top at <paramref name="top"/>, aligned in the box from
    /// <paramref name="left"/> to <paramref name="right"/>.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="block"></param>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <param name="top"></param>
    /// <param name="alignment"><c>center</c>, <c>start</c> or <c>end</c>.</param>
    /// <param name="color"></param>
    public void Draw(SKCanvas canvas, TextBlock block, float left, float right, float top, string alignment, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        // The text is centered in each line box by the first font's ascent and descent.
        var baselineOffset = ((block.LineHeight - (block.Descent - block.Ascent)) / 2) - block.Ascent;

        for (var i = 0; i < block.Lines.Count; i++)
        {
            var line = block.Lines[i];
            var startIsRight = block.RightToLeft ? alignment != MockupSpecRules.Alignments.End : alignment == MockupSpecRules.Alignments.End;

            var x = alignment == MockupSpecRules.Alignments.Center
                ? left + ((right - left - line.Width) / 2)
                : startIsRight ? right - line.Width : left;

            var baseline = top + (i * block.LineHeight) + baselineOffset;

            foreach (var run in line.Runs)
            {
                using var font = Font(run.Typeface, block.Size);

                canvas.DrawShapedText(Shaper(run.Typeface), run.Text, x, baseline, font, paint);

                x += run.Width;
            }
        }
    }

    /// <summary>
    /// Whether the paragraph's first strong character is right to left (Hebrew, Arabic and their presentation forms).
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static bool IsRightToLeft(string text)
    {
        foreach (var rune in (text ?? string.Empty).EnumerateRunes())
        {
            if (IsRightToLeftRune(rune))
                return true;

            if (Rune.IsLetter(rune))
                return false;
        }

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var shaper in _shapers.Values)
            shaper.Dispose();

        _shapers.Clear();
    }

    private float Measure(string text, List<SKTypeface> chain, float size) => Itemize(text, chain).Sum(run => RunWidth(run.Text, run.Typeface, size));

    private TextLine LayoutLine(string text, List<SKTypeface> chain, float size, bool rightToLeft)
    {
        var runs = Itemize(text, chain).Select(run => new TextRun(run.Text, run.Typeface, RunWidth(run.Text, run.Typeface, size))).ToList();

        if (rightToLeft)
            runs.Reverse();

        return new TextLine(runs, runs.Sum(r => r.Width));
    }

    private float RunWidth(string text, SKTypeface typeface, float size)
    {
        using var font = Font(typeface, size);

        return Shaper(typeface).Shape(text, font).Width;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into runs of one typeface: each character takes the first typeface of the chain that
    /// has it; spaces, punctuation shared by scripts and combining marks stay in the current run.
    /// </summary>
    private static List<(string Text, SKTypeface Typeface)> Itemize(string text, List<SKTypeface> chain)
    {
        var runs = new List<(string Text, SKTypeface Typeface)>();
        var builder = new StringBuilder();
        SKTypeface current = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var neutral = Rune.IsWhiteSpace(rune) || Rune.GetUnicodeCategory(rune) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark;
            var typeface = neutral && current != null ? current : chain.FirstOrDefault(t => t.ContainsGlyph(rune.Value)) ?? current ?? chain[0];

            if (current != null && typeface != current)
            {
                runs.Add((builder.ToString(), current));
                builder.Clear();
            }

            current = typeface;
            builder.Append(rune.ToString());
        }

        if (builder.Length > 0)
            runs.Add((builder.ToString(), current));

        return runs;
    }

    private SKShaper Shaper(SKTypeface typeface)
    {
        if (!_shapers.TryGetValue(typeface, out var shaper))
        {
            shaper = new SKShaper(typeface);
            _shapers[typeface] = shaper;
        }

        return shaper;
    }

    private static SKFont Font(SKTypeface typeface, float size) => new(typeface, size) { Subpixel = true, Edging = SKFontEdging.Antialias, Hinting = SKFontHinting.None };

    private static (float Ascent, float Descent) Metrics(SKTypeface typeface, float size)
    {
        using var font = Font(typeface, size);

        var metrics = font.Metrics;

        return (metrics.Ascent, metrics.Descent);
    }

    private static bool IsRightToLeftRune(Rune rune) => rune.Value is (>= 0x0590 and <= 0x08FF) or (>= 0xFB1D and <= 0xFDFF) or (>= 0xFE70 and <= 0xFEFF);
}

/// <summary>
/// Laid out caption text.
/// </summary>
/// <param name="Lines">Lines in order.</param>
/// <param name="Size">Font size in pixels.</param>
/// <param name="LineHeight">Line height in pixels.</param>
/// <param name="RightToLeft">Whether the paragraph is right to left.</param>
/// <param name="FontMetrics">Ascent (negative) and descent of the first font.</param>
public sealed record TextBlock(IReadOnlyList<TextLine> Lines, float Size, float LineHeight, bool RightToLeft, (float Ascent, float Descent) FontMetrics)
{
    /// <summary>
    /// A block without text.
    /// </summary>
    public static TextBlock Empty { get; } = new([], 0, 0, false, (0, 0));

    /// <summary>
    /// Height of the block.
    /// </summary>
    public float Height => this.Lines.Count * this.LineHeight;

    /// <summary>
    /// Ascent of the first font (negative).
    /// </summary>
    public float Ascent => this.FontMetrics.Ascent;

    /// <summary>
    /// Descent of the first font.
    /// </summary>
    public float Descent => this.FontMetrics.Descent;
}

/// <summary>
/// A line of runs in visual order.
/// </summary>
/// <param name="Runs"></param>
/// <param name="Width"></param>
public sealed record TextLine(IReadOnlyList<TextRun> Runs, float Width);

/// <summary>
/// Text drawn with one typeface.
/// </summary>
/// <param name="Text"></param>
/// <param name="Typeface"></param>
/// <param name="Width"></param>
public sealed record TextRun(string Text, SKTypeface Typeface, float Width);
