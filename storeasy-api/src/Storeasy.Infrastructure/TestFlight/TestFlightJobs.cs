using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.TestFlight;

/// <summary>
/// Shared start of the TestFlight jobs.
/// </summary>
public static class TestFlightJobs
{
    /// <summary>
    /// The App Store record of the job with its opened connection; a missing record or connection fails the job.
    /// </summary>
    public static async Task<(StoreApp StoreApp, OpenedStoreConnection Connection)> OpenAsync(IStoreasyRepositoryBase<StoreApp> storeAppRepository, IStoreJobContext context, int storeAppId, CancellationToken cancellationToken)
    {
        var storeApp = await storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == storeAppId && s.OrganizationId == context.OrganizationId && s.Store == StoreKind.Apple,
                                                                       projection: s => new StoreApp { Id = s.Id, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId },
                                                                       cancellationToken: cancellationToken) ?? throw new StoreJobException("The App Store record was removed.", transient: false);

        var connection = storeApp.StoreConnectionId.HasValue ? await context.OpenConnectionAsync(storeApp.StoreConnectionId.Value, cancellationToken) : null;

        return (storeApp, connection ?? throw new StoreJobException("The App Store record has no connection.", transient: false));
    }
}
