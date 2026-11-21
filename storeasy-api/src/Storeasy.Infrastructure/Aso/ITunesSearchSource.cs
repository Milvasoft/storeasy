using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// App Store search results from the public iTunes Search API (software only, up to 200 results). Its order is close to,
/// but not exactly, the order the App Store app shows; ranks are read from it the same way every day.
/// </summary>
/// <param name="http"></param>
public sealed class ITunesSearchSource(AsoHttp http) : IAsoSearchSource
{
    /// <summary>
    /// Endpoint of the search.
    /// </summary>
    public const string Endpoint = "https://itunes.apple.com/search";

    /// <summary>
    /// Most results the API returns.
    /// </summary>
    public const int MaxResults = 200;

    private readonly AsoHttp _http = http;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Apple;

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public async Task<AsoSearchResult> SearchAsync(string term, string country, string language, int depth, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(country))
            return null;

        var limit = Math.Clamp(depth, 1, MaxResults);
        var url = new Uri($"{Endpoint}?term={Uri.EscapeDataString(term)}&country={Uri.EscapeDataString(country)}&entity=software&limit={limit}");
        var body = await _http.GetStringAsync(url, null, cancellationToken);

        return body == null ? null : AsoResponseParsers.ParseITunesSearch(body);
    }
}
