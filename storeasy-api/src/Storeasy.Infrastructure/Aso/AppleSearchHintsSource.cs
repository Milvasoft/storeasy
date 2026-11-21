using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// App Store search hints: the autocomplete the App Store shows while a term is typed, read from the public hints endpoint
/// with the country's storefront header. Countries without a known storefront id are not supported (the endpoint would
/// answer for another country).
/// </summary>
/// <param name="http"></param>
public sealed class AppleSearchHintsSource(AsoHttp http) : IAsoSuggestionSource
{
    /// <summary>
    /// Source name kept with a measurement.
    /// </summary>
    public const string SourceName = "apple-search-hints";

    /// <summary>
    /// Endpoint of the hints.
    /// </summary>
    public const string Endpoint = "https://search.itunes.apple.com/WebObjects/MZSearchHints.woa/wa/hints";

    private readonly AsoHttp _http = http;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Apple;

    /// <inheritdoc/>
    public string Name => SourceName;

    /// <inheritdoc/>
    public bool Supports(string country) => AsoCountries.AppleStorefront(country) != null;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SuggestAsync(string term, string country, string language, CancellationToken cancellationToken)
    {
        var storefront = AsoCountries.AppleStorefront(country);

        if (storefront == null || string.IsNullOrWhiteSpace(term))
            return null;

        var url = new Uri($"{Endpoint}?clientApplication=Software&term={Uri.EscapeDataString(term)}");
        var body = await _http.GetStringAsync(url, new Dictionary<string, string> { ["X-Apple-Store-Front"] = $"{storefront}-1,29" }, cancellationToken);

        return body == null ? null : AsoResponseParsers.ParseAppleHints(body);
    }
}
