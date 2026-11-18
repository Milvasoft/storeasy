using SkiaSharp;
using Storeasy.Domain.Studio;
using System.Collections.Concurrent;
using System.Globalization;

namespace Storeasy.Infrastructure.Studio.Rendering;

/// <summary>
/// The fonts the studio draws with, all SIL OFL 1.1 and shipped with the application (<c>StudioFonts/</c>): Inter and Noto
/// Sans for Latin, Greek and Cyrillic, Noto Sans Arabic, Hebrew, Thai and Devanagari, and Noto Sans CJK (JP, KR, SC, TC).
/// Typefaces load from their files on first use and are shared (immutable, thread safe). No system font is ever used, so a
/// render looks the same on every host.
/// </summary>
/// <param name="folder">Folder with the <c>Inter</c>, <c>NotoSans</c> and <c>NotoSansCJK</c> subfolders.</param>
public sealed class StudioFontLibrary(string folder) : IDisposable
{
    /// <summary>
    /// Default folder name next to the application.
    /// </summary>
    public const string DefaultFolderName = "StudioFonts";

    private static readonly Dictionary<string, Dictionary<int, string>> _files = new(StringComparer.Ordinal)
    {
        [MockupSpecRules.Fonts.Inter] = Weights("Inter/Inter-{0}.ttf", 400, 500, 600, 700, 800),
        [MockupSpecRules.Fonts.NotoSans] = Weights("NotoSans/NotoSans-{0}.ttf", 400, 500, 600, 700, 800),
        [ScriptFamilies.Arabic] = Weights("NotoSans/NotoSansArabic-{0}.ttf", 400, 700),
        [ScriptFamilies.Hebrew] = Weights("NotoSans/NotoSansHebrew-{0}.ttf", 400, 700),
        [ScriptFamilies.Thai] = Weights("NotoSans/NotoSansThai-{0}.ttf", 400, 700),
        [ScriptFamilies.Devanagari] = Weights("NotoSans/NotoSansDevanagari-{0}.ttf", 400, 700),
        [ScriptFamilies.Japanese] = Weights("NotoSansCJK/NotoSansJP-{0}.otf", 400, 700),
        [ScriptFamilies.Korean] = Weights("NotoSansCJK/NotoSansKR-{0}.otf", 400, 700),
        [ScriptFamilies.SimplifiedChinese] = Weights("NotoSansCJK/NotoSansSC-{0}.otf", 400, 700),
        [ScriptFamilies.TraditionalChinese] = Weights("NotoSansCJK/NotoSansTC-{0}.otf", 400, 700),
    };

    private readonly string _folder = folder;
    private readonly ConcurrentDictionary<string, SKTypeface> _typefaces = new(StringComparer.Ordinal);

    /// <summary>
    /// Fallback families of the scripts the primary families lack.
    /// </summary>
    public static class ScriptFamilies
    {
        /// <summary>Noto Sans Arabic.</summary>
        public const string Arabic = "noto-sans-arabic";

        /// <summary>Noto Sans Hebrew.</summary>
        public const string Hebrew = "noto-sans-hebrew";

        /// <summary>Noto Sans Thai.</summary>
        public const string Thai = "noto-sans-thai";

        /// <summary>Noto Sans Devanagari.</summary>
        public const string Devanagari = "noto-sans-devanagari";

        /// <summary>Noto Sans CJK JP.</summary>
        public const string Japanese = "noto-sans-jp";

        /// <summary>Noto Sans CJK KR.</summary>
        public const string Korean = "noto-sans-kr";

        /// <summary>Noto Sans CJK SC.</summary>
        public const string SimplifiedChinese = "noto-sans-sc";

        /// <summary>Noto Sans CJK TC.</summary>
        public const string TraditionalChinese = "noto-sans-tc";
    }

    /// <summary>
    /// Folder of the font files.
    /// </summary>
    public string Folder => _folder;

    /// <summary>
    /// The default font folder: <c>StudioFonts</c> next to the application.
    /// </summary>
    /// <param name="configured">Configured folder; empty for the default.</param>
    /// <returns></returns>
    public static string ResolveFolder(string configured)
        => string.IsNullOrWhiteSpace(configured) ? Path.Combine(AppContext.BaseDirectory, DefaultFolderName) : configured;

    /// <summary>
    /// Embedded weights of <paramref name="family"/>.
    /// </summary>
    /// <param name="family"></param>
    /// <returns></returns>
    public static IReadOnlyCollection<int> WeightsOf(string family) => _files.TryGetValue(family, out var weights) ? weights.Keys : [];

    /// <summary>
    /// Every font file the library may load, relative to <see cref="Folder"/>.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> AllFiles() => _files.Values.SelectMany(w => w.Values);

    /// <summary>
    /// The embedded weight of <paramref name="family"/> nearest to <paramref name="weight"/> (the heavier one on a tie).
    /// </summary>
    /// <param name="family"></param>
    /// <param name="weight"></param>
    /// <returns></returns>
    public static int NearestWeight(string family, int weight)
        => WeightsOf(family).OrderBy(w => Math.Abs(w - weight)).ThenByDescending(w => w).FirstOrDefault();

    /// <summary>
    /// The typeface of <paramref name="family"/> in its embedded weight nearest to <paramref name="weight"/>.
    /// </summary>
    /// <param name="family"></param>
    /// <param name="weight"></param>
    /// <returns></returns>
    public SKTypeface Get(string family, int weight)
    {
        if (!_files.TryGetValue(family, out var weights))
            throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown studio font family.");

        var file = weights[NearestWeight(family, weight)];

        return _typefaces.GetOrAdd(file, Load);
    }

    /// <summary>
    /// Families to try in order for a caption in <paramref name="locale"/>: the template's family, Noto Sans, the CJK
    /// family of the locale's writing (Japanese, Korean, Traditional or Simplified Chinese) before the other CJK families,
    /// then Arabic, Hebrew, Thai and Devanagari.
    /// </summary>
    /// <param name="primary"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public static IReadOnlyList<string> FallbackChain(string primary, string locale)
    {
        var chain = new List<string> { primary };

        if (primary != MockupSpecRules.Fonts.NotoSans)
            chain.Add(MockupSpecRules.Fonts.NotoSans);

        chain.AddRange(CjkOrder(locale));
        chain.AddRange([ScriptFamilies.Arabic, ScriptFamilies.Hebrew, ScriptFamilies.Thai, ScriptFamilies.Devanagari]);

        return chain;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var typeface in _typefaces.Values)
            typeface.Dispose();

        _typefaces.Clear();
    }

    private static string[] CjkOrder(string locale)
    {
        var tag = (locale ?? string.Empty).ToLowerInvariant();

        if (tag.StartsWith("ja"))
            return [ScriptFamilies.Japanese, ScriptFamilies.SimplifiedChinese, ScriptFamilies.TraditionalChinese, ScriptFamilies.Korean];

        if (tag.StartsWith("ko"))
            return [ScriptFamilies.Korean, ScriptFamilies.Japanese, ScriptFamilies.SimplifiedChinese, ScriptFamilies.TraditionalChinese];

        if (tag.StartsWith("zh") && (tag.Contains("hant") || tag.EndsWith("-tw") || tag.EndsWith("-hk") || tag.EndsWith("-mo")))
            return [ScriptFamilies.TraditionalChinese, ScriptFamilies.SimplifiedChinese, ScriptFamilies.Japanese, ScriptFamilies.Korean];

        return [ScriptFamilies.SimplifiedChinese, ScriptFamilies.TraditionalChinese, ScriptFamilies.Japanese, ScriptFamilies.Korean];
    }

    private SKTypeface Load(string file)
    {
        var path = Path.Combine(_folder, file);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Studio font '{file}' was not found in '{_folder}' (Storeasy:Studio:FontsPath).", path);

        return SKTypeface.FromFile(path) ?? throw new InvalidOperationException($"Studio font '{path}' could not be read.");
    }

    private static Dictionary<int, string> Weights(string pattern, params int[] weights)
        => weights.ToDictionary(w => w, w => string.Format(CultureInfo.InvariantCulture, pattern, WeightName(w)));

    private static string WeightName(int weight) => weight switch
    {
        500 => "Medium",
        600 => "SemiBold",
        700 => "Bold",
        800 => "ExtraBold",
        _ => "Regular",
    };
}
