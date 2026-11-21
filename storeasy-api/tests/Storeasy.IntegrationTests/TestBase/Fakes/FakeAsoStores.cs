using System.Collections.Concurrent;
using System.Net;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The stores' public search endpoints as the ASO sources see them: App Store search hints (property list), the iTunes
/// Search API, Google Play suggest and the Google Play search page. No test reaches a store. A term containing
/// <see cref="OfflineMarker"/> answers 503. Tests use unique terms, because the answers are cached for the whole process.
/// </summary>
public sealed class FakeAsoStores : HttpMessageHandler
{
    /// <summary>A term with this part makes every endpoint answer 503.</summary>
    public const string OfflineMarker = "offline";

    /// <summary>Search hints by exact term (lower case); unknown terms get the default hints.</summary>
    public ConcurrentDictionary<string, string[]> Hints { get; } = new(StringComparer.Ordinal);

    /// <summary>Search results by term (lower case): the apps in order with id, name, rating count and rating.</summary>
    public ConcurrentDictionary<string, (string Id, string Name, long RatingCount, double Rating)[]> Results { get; } = new(StringComparer.Ordinal);

    /// <summary>Requests received: host, path and the decoded query.</summary>
    public ConcurrentQueue<(string Host, string Path, Dictionary<string, string> Query, string UserAgent, string Storefront)> Requests { get; } = new();

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var parsed = HttpUtility.ParseQueryString(uri.Query);
        var query = parsed.AllKeys.Where(k => k != null).ToDictionary(k => k, k => parsed[k], StringComparer.Ordinal);
        var storefront = request.Headers.TryGetValues("X-Apple-Store-Front", out var values) ? values.FirstOrDefault() : null;

        Requests.Enqueue((uri.Host, uri.AbsolutePath, query, request.Headers.UserAgent.ToString(), storefront));

        var term = (query.GetValueOrDefault("term") ?? query.GetValueOrDefault("query") ?? query.GetValueOrDefault("q") ?? string.Empty).ToLowerInvariant();

        if (term.Contains(OfflineMarker, StringComparison.Ordinal))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        return Task.FromResult(uri.Host switch
        {
            "search.itunes.apple.com" => Text(Plist(HintsOf(term)), "text/xml"),
            "itunes.apple.com" => Text(ITunes(ResultsOf(term)), "application/json"),
            "market.android.com" => Text(JsonSerializer.Serialize(HintsOf(term).Select(h => new { s = h, t = "q" })), "application/json"),
            "play.google.com" => Text(PlayPage(ResultsOf(term)), "text/html"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
    }

    // By default a term of three or more letters is offered first for its own prefixes of three or more letters.
    private string[] HintsOf(string term)
    {
        if (Hints.TryGetValue(term, out var hints))
            return hints;

        var match = Hints.Keys.Where(k => k.StartsWith(term, StringComparison.Ordinal) && term.Length >= 3).OrderBy(k => k.Length).FirstOrDefault();

        return match != null ? [match, $"{match} pro"] : [$"{term} app", $"{term} free"];
    }

    private (string Id, string Name, long RatingCount, double Rating)[] ResultsOf(string term)
        => Results.TryGetValue(term, out var results) ? results : [("1001", $"{term} One", 50000, 4.5), ("1002", $"{term} Two", 900, 4.1), ("1003", "Other App", 12, 3.9)];

    private static string Plist(string[] hints)
    {
        var builder = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?><!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd"><plist version="1.0"><dict><key>title</key><string>Suggestions</string><key>hints</key><array>""");

        foreach (var hint in hints)
            builder.Append("<dict><key>term</key><string>").Append(SecurityElement.Escape(hint)).Append("</string></dict>");

        return builder.Append("</array></dict></plist>").ToString();
    }

    private static string ITunes((string Id, string Name, long RatingCount, double Rating)[] results)
        => JsonSerializer.Serialize(new
        {
            resultCount = results.Length,
            results = results.Select(r => new
            {
                wrapperType = "software",
                kind = "software",
                trackId = r.Id,
                trackName = r.Name,
                sellerName = "Developer",
                averageUserRating = r.Rating,
                userRatingCount = r.RatingCount,
                artworkUrl100 = $"https://is1-ssl.mzstatic.test/{r.Id}/100x100bb.jpg",
            }),
        });

    private static string PlayPage((string Id, string Name, long RatingCount, double Rating)[] results)
        => $"<html><body>{string.Concat(results.Select(r => $"<a href=\"/store/apps/details?id=com.test.{r.Id}\">{r.Name}</a>"))}</body></html>";

    private static HttpResponseMessage Text(string body, string type) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, type) };
}
