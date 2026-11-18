using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using System.Net;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// Helpers of the subscription and In-App Purchase tests: planning through the money endpoints and waiting for the
/// sync job an applied plan queues.
/// </summary>
public abstract class MonetizationTestBase(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    protected static async Task<(HttpStatusCode Status, ChangePlanDto Plan, string Message)> PlanAsync(TestUser user, string path, int organizationId, int storeAppId, params object[] changes)
    {
        var (http, plan) = await user.Client.PostAsync<ChangePlanDto>(path, new { organizationId, storeAppId, changes });

        return (http.StatusCode, plan?.Data, plan?.MessagesToString());
    }

    /// <summary>
    /// Waits for the newest sync job of a store record to finish and returns it.
    /// </summary>
    protected async Task<StoreJob> WaitForSyncAsync(int organizationId, DateTime after)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking()
                                                               .Where(j => j.OrganizationId == organizationId && j.Kind == StoreJobKind.SyncMonetization && j.CreationDate >= after)
                                                               .OrderByDescending(j => j.Id)
                                                               .FirstOrDefaultAsync());

            if (job != null)
                return await WaitForJobAsync(job.Id);

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("No sync job was queued.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Queues a sync of a store record through the endpoint and waits for it.
    /// </summary>
    protected async Task<StoreJob> SyncAsync(TestUser user, int organizationId, int storeAppId)
    {
        var (_, job) = await user.Client.PostAsync<long>("subscriptions/sync", new { organizationId, storeAppId });

        return await WaitForJobAsync(job.Data);
    }
}
