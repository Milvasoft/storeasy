using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// An organization with an owner, an App Store Connect connection and apps, for the events phase tests.
/// </summary>
public sealed class EventOrg
{
    /// <summary>Owner.</summary>
    public TestUser Owner { get; init; }

    /// <summary>Organization.</summary>
    public int OrganizationId { get; init; }

    /// <summary>App Store Connect connection.</summary>
    public int AppleConnectionId { get; init; }

    /// <summary>Google Play connection.</summary>
    public int GoogleConnectionId { get; init; }

    /// <summary>Unique suffix for store ids.</summary>
    public string Suffix { get; init; }
}

/// <summary>
/// Helpers of the events phase tests.
/// </summary>
public static class EventTestKit
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates an owner, an organization and an Apple and a Google connection.
    /// </summary>
    public static async Task<EventOrg> NewOrgAsync(this StoreasyApiFactory factory)
    {
        var owner = await factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var keyId = string.Concat(RandomNumberGenerator.GetItems<char>("ABCDEFGHJKLMNPQRSTUVWXYZ0123456789", 10));

        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var rsa = RSA.Create(2048);

        var appleSecret = JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId, privateKey = ec.ExportPkcs8PrivateKeyPem() });
        var googleSecret = JsonSerializer.Serialize(new Dictionary<string, string> { ["type"] = "service_account", ["project_id"] = "demo", ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(), ["client_email"] = "store@demo.iam.gserviceaccount.com" });

        var (_, apple) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = organization.Data, store = StoreKind.Apple, name = "ASC", secret = appleSecret });
        var (_, google) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = organization.Data, store = StoreKind.Google, name = "Play", secret = googleSecret });

        return new EventOrg { Owner = owner, OrganizationId = organization.Data, AppleConnectionId = apple.Data, GoogleConnectionId = google.Data, Suffix = $"t{TestApi.Unique()}" };
    }

    /// <summary>
    /// Adds an app with a record in <paramref name="store"/> directly to the database; returns (app id, store record id, store's app id).
    /// </summary>
    public static async Task<(int AppId, int StoreAppId, string ExternalId)> AddAppAsync(this StoreasyApiFactory factory, EventOrg org, StoreKind store = StoreKind.Apple, string name = null)
    {
        var externalId = store == StoreKind.Apple ? $"6{Random.Shared.Next(100000, 999999)}{org.Suffix}" : $"com.{org.Suffix}.{Random.Shared.Next(1000, 9999)}";
        var storeApp = new StoreApp { OrganizationId = org.OrganizationId, Store = store, ExternalId = externalId, BundleId = $"com.{org.Suffix}.app", Name = name ?? "Defter", StoreConnectionId = store == StoreKind.Apple ? org.AppleConnectionId : org.GoogleConnectionId, SyncedDate = DateTime.UtcNow };
        var app = new App { OrganizationId = org.OrganizationId, Name = name ?? "Defter", StoreApps = [storeApp], CreationDate = DateTime.UtcNow };

        await factory.DbAsync(async db =>
        {
            db.Apps.Add(app);
            await db.SaveChangesAsync();
        });

        return (app.Id, storeApp.Id, externalId);
    }

    /// <summary>
    /// Adds a build to a store record directly; returns its id.
    /// </summary>
    public static async Task<long> AddBuildAsync(this StoreasyApiFactory factory, int organizationId, int storeAppId, string externalId, string version, string number, DateTime? expiry, BuildProcessingState state = BuildProcessingState.Valid)
    {
        var build = new Build { OrganizationId = organizationId, StoreAppId = storeAppId, Store = StoreKind.Apple, ExternalId = externalId, Version = version, BuildNumber = number, ProcessingState = state, UploadedDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = expiry, SyncedDate = DateTime.UtcNow, CreationDate = DateTime.UtcNow };

        await factory.DbAsync(async db =>
        {
            db.Builds.Add(build);
            await db.SaveChangesAsync();
        });

        return build.Id;
    }

    /// <summary>
    /// Creates a user who is a member of the organization with <paramref name="role"/> and logs it in.
    /// </summary>
    public static async Task<TestUser> AddMemberAsync(this StoreasyApiFactory factory, int organizationId, OrgRole role)
    {
        var user = await factory.CreateUserAsync(email: $"m{TestApi.Unique()}@example.com");

        await factory.DbAsync(async db =>
        {
            db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = organizationId, UserId = user.Id, Role = role, CreationDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        return await factory.LoginAsync(user.UserName, TestApi.DefaultPassword);
    }

    /// <summary>
    /// Waits until a job finishes and returns it.
    /// </summary>
    public static async Task<StoreJob> WaitForJobAsync(this StoreasyApiFactory factory, long jobId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

            if (StoreJobRules.IsFinished(job.Status))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status}.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Waits until every job of <paramref name="kind"/> of the organization finished.
    /// </summary>
    public static async Task WaitForJobsAsync(this StoreasyApiFactory factory, int organizationId, StoreJobKind kind)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (await factory.DbAsync(db => db.StoreJobs.AnyAsync(j => j.OrganizationId == organizationId && j.Kind == kind && (j.Status == StoreJobStatus.Queued || j.Status == StoreJobStatus.Running))))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"{kind} jobs of organization {organizationId} did not finish.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Waits until <paramref name="condition"/> holds.
    /// </summary>
    public static async Task EventuallyAsync(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException(because);

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Queues a job as the system would.
    /// </summary>
    public static Task<List<long>> EnqueueAsync(this StoreasyApiFactory factory, StoreJobRequest request)
        => factory.WithServiceAsync<IStoreJobQueue, List<long>>(queue => queue.EnqueueAsync([request], CancellationToken.None));
}
