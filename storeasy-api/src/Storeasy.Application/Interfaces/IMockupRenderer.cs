using Storeasy.Domain.Studio;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Draws mockups from a template's design (docs/urun-plani.md K8): background, storeasy's own device frame with the
/// screenshot, captions with the embedded open licensed fonts, badges and shapes. Output is a PNG without an alpha
/// channel at the exact class size (or scaled for a preview). Deterministic: the same request gives the same bytes on the
/// same platform. CPU bound and thread safe; registered as a singleton.
/// </summary>
public interface IMockupRenderer
{
    /// <summary>
    /// Renders one image. A failure (raw screenshot too small, unreadable) is returned in the result, not thrown.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    MockupRenderResult Render(MockupRenderRequest request);

    /// <summary>
    /// Lays out the captions of one image without drawing it: lines and sizes after auto fit, and overflow warnings.
    /// </summary>
    /// <param name="spec"></param>
    /// <param name="size">Class size.</param>
    /// <param name="frame">Frame the class is drawn with (decides the text box).</param>
    /// <param name="locale"></param>
    /// <param name="title"></param>
    /// <param name="subtitle"></param>
    /// <returns></returns>
    MockupCaptionMeasure MeasureCaptions(MockupSpec spec, PixelSize size, MockupFrameKind frame, string locale, string title, string subtitle);
}

/// <summary>
/// One image to render.
/// </summary>
public sealed record MockupRenderRequest
{
    /// <summary>
    /// The design (upgraded, see <see cref="MockupSpecRules.Upgrade"/>).
    /// </summary>
    public MockupSpec Spec { get; init; }

    /// <summary>
    /// Class size; the layout is computed at this size.
    /// </summary>
    public PixelSize Size { get; init; }

    /// <summary>
    /// Frame to draw.
    /// </summary>
    public MockupFrameKind Frame { get; init; }

    /// <summary>
    /// Locale of the captions (line breaking, casing, font fallback, direction).
    /// </summary>
    public string Locale { get; init; }

    /// <summary>
    /// Title.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Subtitle.
    /// </summary>
    public string Subtitle { get; init; }

    /// <summary>
    /// Encoded raw screenshot (PNG or JPEG); null for an empty screen.
    /// </summary>
    public byte[] Screenshot { get; init; }

    /// <summary>
    /// Encoded background image for an image background.
    /// </summary>
    public byte[] BackgroundImage { get; init; }

    /// <summary>
    /// Output scale, above 0 and at most 1 (previews render smaller with the same layout).
    /// </summary>
    public float OutputScale { get; init; } = 1;

    /// <summary>
    /// Whether a too small raw screenshot fails the image (export) or is drawn with a warning (preview).
    /// </summary>
    public bool FailWhenTooSmall { get; init; } = true;
}

/// <summary>
/// A rendered image or its failure.
/// </summary>
/// <param name="Png">PNG bytes; null on failure.</param>
/// <param name="Width">Pixel width of the PNG.</param>
/// <param name="Height">Pixel height of the PNG.</param>
/// <param name="Warnings">Warnings of the image.</param>
/// <param name="Failure">Why the image was not produced; null on success.</param>
public sealed record MockupRenderResult(byte[] Png, int Width, int Height, IReadOnlyList<MockupRenderIssue> Warnings, MockupRenderIssue Failure = null);

/// <summary>
/// A warning or failure of a render.
/// </summary>
/// <param name="Code">One of <see cref="MockupWarningCodes"/>.</param>
/// <param name="Message">English detail.</param>
/// <param name="Field"><c>title</c> or <c>subtitle</c> for caption issues.</param>
/// <param name="Lines">Lines the caption needs.</param>
/// <param name="MaxLines">Lines the style allows.</param>
public sealed record MockupRenderIssue(string Code, string Message, string Field = null, int? Lines = null, int? MaxLines = null);

/// <summary>
/// Caption layout of one image.
/// </summary>
/// <param name="TitleLines">Lines of the title.</param>
/// <param name="TitleMaxLines">Lines the title style allows.</param>
/// <param name="TitleSize">Title font size in pixels after auto fit.</param>
/// <param name="SubtitleLines">Lines of the subtitle.</param>
/// <param name="SubtitleMaxLines">Lines the subtitle style allows.</param>
/// <param name="SubtitleSize">Subtitle font size in pixels after auto fit.</param>
/// <param name="Warnings">Overflow and font warnings.</param>
public sealed record MockupCaptionMeasure(int TitleLines, int TitleMaxLines, float TitleSize, int SubtitleLines, int SubtitleMaxLines, float SubtitleSize, IReadOnlyList<MockupRenderIssue> Warnings);
