using Storeasy.Application.Interfaces;
using Storeasy.Domain.JsonModels;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// Readers of the stores' public answers. Each one is tolerant: an answer it does not understand reads as empty (or null
/// for a search), never as an exception, because these endpoints are undocumented and change without notice.
/// </summary>
public static partial class AsoResponseParsers
{
    /// <summary>
    /// The terms of an App Store search hints answer: the property list (XML) the endpoint has long answered with
    /// (<c>hints</c> array of dicts with a <c>term</c> string), or a JSON object with a <c>hints</c> array of objects with
    /// a <c>term</c>.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static List<string> ParseAppleHints(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        var text = body.TrimStart();

        return text.StartsWith('{') ? AppleHintsFromJson(text) : AppleHintsFromPlist(text);
    }

    /// <summary>
    /// An iTunes Search API answer (<c>resultCount</c>, <c>results</c>): the software results in order, with id (trackId),
    /// name, developer, average rating, rating count and icon. Null when the body is not that JSON.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static AsoSearchResult ParseITunesSearch(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return null;

            var apps = new List<AsoCompetitor>();

            foreach (var item in results.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.Object))
            {
                var kind = Text(item, "kind") ?? Text(item, "wrapperType");

                if (kind != null && kind != "software")
                    continue;

                var id = item.TryGetProperty("trackId", out var trackId) && trackId.ValueKind == JsonValueKind.Number ? trackId.GetRawText() : Text(item, "trackId");

                if (string.IsNullOrEmpty(id))
                    continue;

                apps.Add(new AsoCompetitor
                {
                    Rank = apps.Count + 1,
                    Id = id,
                    Name = Text(item, "trackName"),
                    Developer = Text(item, "sellerName") ?? Text(item, "artistName"),
                    Rating = Number(item, "averageUserRating"),
                    RatingCount = Number(item, "userRatingCount") is { } count ? (long)count : 0,
                    IconUrl = Text(item, "artworkUrl100") ?? Text(item, "artworkUrl60"),
                });
            }

            return new AsoSearchResult(apps.Count, apps);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A Google Play suggest answer: a JSON array of <c>{"s": term, "t": type}</c> objects (query suggestions have type
    /// <c>q</c>; app suggestions are left out), or the <c>[query, [terms]]</c> form of the generic suggest endpoints.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static List<string> ParseGooglePlaySuggest(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return [];

            var items = root.EnumerateArray().ToList();

            if (items.Count >= 2 && items[0].ValueKind == JsonValueKind.String && items[1].ValueKind == JsonValueKind.Array)
                return [.. items[1].EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()).Where(t => !string.IsNullOrWhiteSpace(t))];

            return [.. items.Where(i => i.ValueKind == JsonValueKind.Object && (Text(i, "t") ?? "q") == "q")
                            .Select(i => Text(i, "s"))
                            .Where(t => !string.IsNullOrWhiteSpace(t))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The apps of a Google Play search results page, in order: every distinct package of a <c>/store/apps/details?id=</c>
    /// link. Names are not read (the page's markup is generated and changes often). Null when the page has no app link at
    /// all, which is more likely a changed page or a consent wall than a search without results.
    /// </summary>
    /// <param name="html"></param>
    /// <returns></returns>
    public static AsoSearchResult ParseGooglePlaySearch(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var ids = new List<string>();

        foreach (Match match in PlayDetailsLink().Matches(html))
        {
            var id = WebUtility.HtmlDecode(match.Groups["id"].Value);

            if (!ids.Contains(id, StringComparer.Ordinal))
                ids.Add(id);
        }

        if (ids.Count == 0)
            return null;

        return new AsoSearchResult(ids.Count, [.. ids.Select((id, index) => new AsoCompetitor { Rank = index + 1, Id = id })]);
    }

    private static List<string> AppleHintsFromJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Object)
                root = results;

            if (!root.TryGetProperty("hints", out var hints) || hints.ValueKind != JsonValueKind.Array)
                return [];

            return [.. hints.EnumerateArray().Where(h => h.ValueKind == JsonValueKind.Object).Select(h => Text(h, "term")).Where(t => !string.IsNullOrWhiteSpace(t))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> AppleHintsFromPlist(string text)
    {
        try
        {
            // The property list carries a DOCTYPE; it is ignored and nothing external is ever resolved.
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            var document = XDocument.Load(reader);
            var root = document.Root?.Element("dict");

            if (root == null)
                return [];

            var hintsArray = ValueOf(root, "hints");

            if (hintsArray == null || hintsArray.Name != "array")
                return [];

            return [.. hintsArray.Elements("dict").Select(d => ValueOf(d, "term")).Where(v => v?.Name == "string").Select(v => v.Value.Trim()).Where(t => t.Length > 0)];
        }
        catch (XmlException)
        {
            return [];
        }
    }

    // In a plist dict every <key> is followed by its value element.
    private static XElement ValueOf(XElement dict, string key)
        => dict.Elements("key").FirstOrDefault(k => k.Value == key)?.ElementsAfterSelf().FirstOrDefault();

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    [GeneratedRegex("/store/apps/details\\?id=(?<id>[A-Za-z][A-Za-z0-9_]*(?:\\.[A-Za-z0-9_]+)+)")]
    private static partial Regex PlayDetailsLink();
}
