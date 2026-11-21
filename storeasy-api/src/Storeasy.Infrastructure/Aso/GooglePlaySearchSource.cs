using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// Google Play search results read from the public search page (Google Play has no search API). Off unless
/// <see cref="AsoOptions.GooglePlayRanksEnabled"/>: automated reading of the page may be against the terms of service of
/// Google Play, and the page changes without notice, so the operator turns it on knowingly. Only the order of the apps
/// (package names) is read; no ratings, so no difficulty.
/// </summary>
/// <param name="http"></param>
/// <param name="options"></param>
public sealed class GooglePlaySearchSource(AsoHttp http, AsoOptions options) : IAsoSearchSource
{
    /// <summary>
    /// Endpoint of the search page.
    /// </summary>
    public const string Endpoint = "https://play.google.com/store/search";

    private readonly AsoHttp _http = http;
    private readonly AsoOptions _options = options;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Google;

    /// <inheritdoc/>
    public bool IsEnabled => _options.GooglePlayRanksEnabled;

    /// <inheritdoc/>
    public async Task<AsoSearchResult> SearchAsync(string term, string country, string language, int depth, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(country))
            return null;

        var url = new Uri($"{Endpoint}?q={Uri.EscapeDataString(term)}&c=apps&hl={Uri.EscapeDataString(language ?? "en")}&gl={Uri.EscapeDataString(country.ToUpperInvariant())}");
        var body = await _http.GetStringAsync(url, null, cancellationToken);
        var result = body == null ? null : AsoResponseParsers.ParseGooglePlaySearch(body);

        return result == null ? null : new AsoSearchResult(Math.Min(result.ResultCount, depth), [.. result.Apps.Take(depth)]);
    }
}
