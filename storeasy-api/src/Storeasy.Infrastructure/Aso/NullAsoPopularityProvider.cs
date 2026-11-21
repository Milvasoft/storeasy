using Microsoft.Extensions.Logging;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// The account based popularity source while none is available. Apple Search Ads popularity needs the operator's Search Ads
/// credentials (<see cref="AsoOptions.SearchAds"/>) and a client of its Campaign Management API, which this build does not
/// include yet; popularity then comes from the public search suggestions. When credentials are set anyway, this says so
/// once in the log.
/// </summary>
/// <param name="options"></param>
/// <param name="logger"></param>
public sealed class NullAsoPopularityProvider(AsoOptions options, ILogger<NullAsoPopularityProvider> logger) : IAsoPopularityProvider
{
    private static int _warned;

    private readonly AsoOptions _options = options;
    private readonly ILogger<NullAsoPopularityProvider> _logger = logger;

    /// <inheritdoc/>
    public bool IsConfigured => false;

    /// <inheritdoc/>
    public string Name => "apple-search-ads";

    /// <inheritdoc/>
    public Task<int?> GetPopularityAsync(StoreKind store, string keyword, string country, CancellationToken cancellationToken)
    {
        if (_options.SearchAds?.IsConfigured == true && Interlocked.Exchange(ref _warned, 1) == 0)
            _logger.LogWarning("Apple Search Ads credentials are set, but this build has no Search Ads popularity client; the search suggestion estimate is used.");

        return Task.FromResult<int?>(null);
    }
}
