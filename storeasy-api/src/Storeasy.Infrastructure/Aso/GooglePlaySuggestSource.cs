using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using System.Text.Json;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// Google Play search suggestions: the autocomplete the Play Store web search box uses, read from its batchexecute endpoint
/// (rpc <see cref="RpcId"/>, a form POST that only reads), per language and country. The old
/// <c>market.android.com/suggest</c> endpoint is gone (404). The rpc is undocumented and may change; a changed answer reads
/// as unavailable and research degrades to no Google Play popularity. Turned off with
/// <see cref="AsoOptions.GooglePlaySuggestionsEnabled"/>.
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
    public const string Endpoint = "https://play.google.com/_/PlayStoreUi/data/batchexecute";

    /// <summary>
    /// Rpc id of the search box suggestions.
    /// </summary>
    public const string RpcId = "IJ4APc";

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

        var url = new Uri($"{Endpoint}?rpcids={RpcId}&hl={Uri.EscapeDataString(language ?? "en")}&gl={Uri.EscapeDataString(country.ToUpperInvariant())}");
        var body = await _http.PostFormAsync(url, new Dictionary<string, string> { ["f.req"] = Request(term) }, cancellationToken);

        return body == null ? null : AsoResponseParsers.ParseGooglePlaySuggest(body);
    }

    /// <summary>
    /// The <c>f.req</c> form field of a suggestion request for <paramref name="term"/>.
    /// </summary>
    /// <param name="term"></param>
    /// <returns></returns>
    public static string Request(string term)
    {
        var inner = JsonSerializer.Serialize(new object[] { new object[] { null, new[] { term }, new[] { 10 }, new[] { 2 }, 4 } });

        return JsonSerializer.Serialize(new object[] { new object[] { new object[] { RpcId, inner, null, "generic" } } });
    }
}
