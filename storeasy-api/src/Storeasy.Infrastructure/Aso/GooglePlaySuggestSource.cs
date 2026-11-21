using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// Google Play search suggestions from the public suggest endpoint (the autocomplete of the Play Store search box), per
/// language and country. Turned off with <see cref="AsoOptions.GooglePlaySuggestionsEnabled"/>.
/// </summary>
/// <param name="http"></param>
/// <param name="options"></param>
public sealed class GooglePlaySuggestSource(AsoHttp http, AsoOptions options) : IAsoSuggestionSource
{
    /// <summary>
    /// Source name kept with a measurement.
    /// </summary>
    public const string SourceName = "google-play-suggest";

    /// <summary>
    /// Endpoint of the suggestions.
    /// </summary>
    public const string Endpoint = "https://market.android.com/suggest/SuggRequest";

    private readonly AsoHttp _http = http;
    private readonly AsoOptions _options = options;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Google;

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public bool Supports(string country) => _options.GooglePlaySuggestionsEnabled && !string.IsNullOrEmpty(country);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SuggestAsync(string term, string country, string language, CancellationToken cancellationToken)
    {
        if (!Supports(country) || string.IsNullOrWhiteSpace(term))
            return null;

        var url = new Uri($"{Endpoint}?json=1&c=3&query={Uri.EscapeDataString(term)}&hl={Uri.EscapeDataString(language ?? "en")}&gl={Uri.EscapeDataString(country.ToUpperInvariant())}");
        var body = await _http.GetStringAsync(url, null, cancellationToken);

        return body == null ? null : AsoResponseParsers.ParseGooglePlaySuggest(body);
    }
}
