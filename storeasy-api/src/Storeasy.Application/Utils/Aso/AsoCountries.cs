using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// Countries of keyword research: the App Store storefront id the search suggestions need (the <c>X-Apple-Store-Front</c>
/// header) and the language a country's searches are usually typed in, used when a request does not name one.
/// </summary>
public static partial class AsoCountries
{
    private static readonly FrozenDictionary<string, int> _storefronts = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["ae"] = 143481,
        ["ar"] = 143505,
        ["at"] = 143445,
        ["au"] = 143460,
        ["be"] = 143446,
        ["br"] = 143503,
        ["ca"] = 143455,
        ["ch"] = 143459,
        ["cl"] = 143483,
        ["cn"] = 143465,
        ["co"] = 143501,
        ["cz"] = 143489,
        ["de"] = 143443,
        ["dk"] = 143458,
        ["eg"] = 143516,
        ["es"] = 143454,
        ["fi"] = 143447,
        ["fr"] = 143442,
        ["gb"] = 143444,
        ["gr"] = 143448,
        ["hk"] = 143463,
        ["hu"] = 143482,
        ["id"] = 143476,
        ["ie"] = 143449,
        ["il"] = 143491,
        ["in"] = 143467,
        ["it"] = 143450,
        ["jp"] = 143462,
        ["kr"] = 143466,
        ["mx"] = 143468,
        ["my"] = 143473,
        ["nl"] = 143452,
        ["no"] = 143457,
        ["nz"] = 143461,
        ["ph"] = 143474,
        ["pl"] = 143478,
        ["pt"] = 143453,
        ["ro"] = 143487,
        ["ru"] = 143469,
        ["sa"] = 143479,
        ["se"] = 143456,
        ["sg"] = 143464,
        ["th"] = 143475,
        ["tr"] = 143480,
        ["tw"] = 143470,
        ["ua"] = 143492,
        ["us"] = 143441,
        ["vn"] = 143471,
        ["za"] = 143472,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> _languages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ae"] = "ar",
        ["ar"] = "es",
        ["at"] = "de",
        ["be"] = "nl",
        ["br"] = "pt",
        ["ch"] = "de",
        ["cl"] = "es",
        ["cn"] = "zh",
        ["co"] = "es",
        ["cz"] = "cs",
        ["de"] = "de",
        ["dk"] = "da",
        ["eg"] = "ar",
        ["es"] = "es",
        ["fi"] = "fi",
        ["fr"] = "fr",
        ["gr"] = "el",
        ["hk"] = "zh",
        ["hu"] = "hu",
        ["id"] = "id",
        ["il"] = "he",
        ["it"] = "it",
        ["jp"] = "ja",
        ["kr"] = "ko",
        ["mx"] = "es",
        ["my"] = "ms",
        ["nl"] = "nl",
        ["no"] = "nb",
        ["pl"] = "pl",
        ["pt"] = "pt",
        ["ro"] = "ro",
        ["ru"] = "ru",
        ["sa"] = "ar",
        ["se"] = "sv",
        ["th"] = "th",
        ["tr"] = "tr",
        ["tw"] = "zh",
        ["ua"] = "uk",
        ["vn"] = "vi",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The country code in lower case when it is two ASCII letters, else null.
    /// </summary>
    /// <param name="country"></param>
    /// <returns></returns>
    public static string Normalize(string country)
    {
        var value = country?.Trim().ToLowerInvariant();

        return value != null && CountryCode().IsMatch(value) ? value : null;
    }

    /// <summary>
    /// The App Store storefront id of a country, or null when it is not known here.
    /// </summary>
    /// <param name="country">Lower case country code.</param>
    /// <returns></returns>
    public static int? AppleStorefront(string country) => country != null && _storefronts.TryGetValue(country, out var id) ? id : null;

    /// <summary>
    /// The usual search language of a country (two letters), English when not known.
    /// </summary>
    /// <param name="country">Lower case country code.</param>
    /// <returns></returns>
    public static string Language(string country) => country != null && _languages.TryGetValue(country, out var language) ? language : "en";

    /// <summary>
    /// The language to use: the requested one when it looks like a language code, else the country's.
    /// </summary>
    /// <param name="requested"></param>
    /// <param name="country"></param>
    /// <returns></returns>
    public static string LanguageOf(string requested, string country)
    {
        var value = requested?.Trim();

        return !string.IsNullOrEmpty(value) && LanguageCode().IsMatch(value) ? value.Split('-', '_')[0].ToLowerInvariant() : Language(country);
    }

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex CountryCode();

    [GeneratedRegex("^[A-Za-z]{2,3}([-_][A-Za-z0-9]{2,8})*$")]
    private static partial Regex LanguageCode();
}
