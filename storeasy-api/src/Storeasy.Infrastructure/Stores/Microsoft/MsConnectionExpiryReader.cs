using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Domain.Stores;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// <see cref="IMsConnectionExpiryReader"/> over the connection rows.
/// </summary>
/// <param name="connectionRepository"></param>
/// <param name="timeProvider"></param>
public sealed class MsConnectionExpiryReader(IStoreasyRepositoryBase<StoreConnection> connectionRepository, TimeProvider timeProvider) : IMsConnectionExpiryReader
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Task<List<MsExpiringConnectionDto>> GetExpiringAsync(DateTime until, int? organizationId, CancellationToken cancellationToken)
        => _connectionRepository.GetExpiringMsConnectionsAsync(until, _timeProvider.GetUtcNow().UtcDateTime, organizationId, cancellationToken);
}
