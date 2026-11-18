using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Apps, store records, builds and the sync job against the fake store engine.")]
public class AppsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private sealed record Setup(TestUser Owner, int OrganizationId, string KeyId, int AppleConnectionId, string Suffix);

    private static string NewKeyId() => string.Concat(RandomNumberGenerator.GetItems<char>("ABCDEFGHJKLMNPQRSTUVWXYZ0123456789", 10));

    private static string AppleSecret(string keyId)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId, privateKey = key.ExportPkcs8PrivateKeyPem() });
    }

    private static string GoogleSecret()
    {
        using var key = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "demo",
            ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "store@demo.iam.gserviceaccount.com",
        });
    }

    private async Task<Setup> NewSetupAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var keyId = NewKeyId();
        var (_, connection) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = organization.Data, store = StoreKind.Apple, name = "ASC", secret = AppleSecret(keyId) });

        return new Setup(owner, organization.Data, keyId, connection.Data, $"t{TestApi.Unique()}");
    }

    private async Task<int> AddGoogleConnectionAsync(Setup setup)
    {
        var (_, connection) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Google, name = "Play", secret = GoogleSecret() });

        return connection.Data;
    }

    private async Task<StoreJob> WaitForJobAsync(long jobId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

            if (StoreJobRules.IsFinished(job.Status))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status}.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private async Task SyncAsync(Setup setup, int? connectionId = null)
    {
        var (http, jobs) = await setup.Owner.Client.PostAsync<List<long>>("apps/sync", new { organizationId = setup.OrganizationId, connectionId });

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var jobId in jobs.Data)
            (await WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    private async Task<List<AppListDto>> ListAppsAsync(Setup setup)
    {
        var (_, apps) = await setup.Owner.Client.PatchListAsync<AppListDto>("apps", new { organizationId = setup.OrganizationId, pageNumber = 1, rowCount = 50 });

        return apps.Data;
    }

    private void GiveAppleApps(Setup setup, params StoreAppRecord[] apps) => _factory.StoreGateway.AppleApps[setup.KeyId] = [.. apps];

    private static StoreBuildRecord AppleBuild(string id, string version, string number, BuildProcessingState state = BuildProcessingState.Valid)
        => new(id, version, number, state, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(89), null, "17.0");

    [Fact]
    public async Task Sync_ShouldBringTheAppleAppsAndTheirBuilds()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var one = new StoreAppRecord($"1{setup.Suffix}", $"com.{setup.Suffix}.one", "One");
        var two = new StoreAppRecord($"2{setup.Suffix}", $"com.{setup.Suffix}.two", "Two");

        GiveAppleApps(setup, one, two);
        _factory.StoreGateway.Builds[one.ExternalId] = [AppleBuild($"b1{setup.Suffix}", "1.0.0", "1", BuildProcessingState.Processing), AppleBuild($"b2{setup.Suffix}", "1.0.0", "2")];

        // Act
        await SyncAsync(setup);

        _factory.StoreGateway.Builds[one.ExternalId] = [AppleBuild($"b1{setup.Suffix}", "1.0.0", "1"), AppleBuild($"b2{setup.Suffix}", "1.0.0", "2")];

        await SyncAsync(setup, setup.AppleConnectionId);

        var apps = await ListAppsAsync(setup);
        var oneApp = apps.Single(a => a.Name == "One");
        var (_, detail) = await setup.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={setup.OrganizationId}&appId={oneApp.Id}");

        // Assert
        apps.Select(a => a.Name).Should().BeEquivalentTo(["One", "Two"]);
        oneApp.Stores.Should().ContainSingle().Which.Should().Match<AppStoreRecordDto>(s => s.Store == StoreKind.Apple && s.ExternalId == one.ExternalId && s.BundleId == one.BundleId && s.SyncedDate != null);
        detail.Data.Builds.Should().HaveCount(2, "a second sync updates instead of duplicating");
        detail.Data.Builds.Should().OnlyContain(b => b.ProcessingState == BuildProcessingState.Valid && b.Version == "1.0.0" && b.ExpiryDate != null);
    }

    [Fact]
    public async Task AddGoogleApp_ShouldLinkToTheAppleRecordWithTheSameIdentifier()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var packageName = $"com.{setup.Suffix}.linked";

        GiveAppleApps(setup, new StoreAppRecord($"3{setup.Suffix}", packageName, "Linked"));
        await SyncAsync(setup);

        var googleConnectionId = await AddGoogleConnectionAsync(setup);
        _factory.StoreGateway.GoogleApps[packageName] = new StoreAppRecord(packageName, packageName, "Linked for Android");
        _factory.StoreGateway.Builds[packageName] = [new StoreBuildRecord("12", "1.2.0", "12", BuildProcessingState.Valid, null, null, "internal,production", null)];

        var appleAppId = (await ListAppsAsync(setup)).Single().Id;

        // Act
        var (http, added) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = googleConnectionId, packageName });
        var job = await WaitForJobAsync(added.Data.JobId);
        var (_, detail) = await setup.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={setup.OrganizationId}&appId={appleAppId}");
        var (duplicate, _) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = googleConnectionId, packageName });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        added.Data.AppId.Should().Be(appleAppId);
        job.Status.Should().Be(StoreJobStatus.Succeeded);
        detail.Data.Stores.Select(s => s.Store).Should().Equal(StoreKind.Apple, StoreKind.Google);
        detail.Data.Stores.Single(s => s.Store == StoreKind.Google).Name.Should().Be("Linked for Android");
        detail.Data.Builds.Should().Contain(b => b.Store == StoreKind.Google && b.BuildNumber == "12" && b.Tracks == "internal,production");
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddGoogleApp_RefusedByPlay_ShouldBeRemovedByTheSyncJob()
    {
        var setup = await NewSetupAsync();
        var googleConnectionId = await AddGoogleConnectionAsync(setup);
        var packageName = $"com.{setup.Suffix}.notinvited";

        var (_, added) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = googleConnectionId, packageName });
        var job = await WaitForJobAsync(added.Data.JobId);
        var (detail, _) = await setup.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={setup.OrganizationId}&appId={added.Data.AppId}");

        job.Status.Should().Be(StoreJobStatus.Succeeded);
        job.Result.Should().Contain(packageName);
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkAndUnlink_ShouldMoveStoreRecordsBetweenApps()
    {
        // Arrange: an Apple app and an unrelated Play app.
        var setup = await NewSetupAsync();
        GiveAppleApps(setup, new StoreAppRecord($"4{setup.Suffix}", $"com.{setup.Suffix}.ios", "iOS name"));
        await SyncAsync(setup);

        var googleConnectionId = await AddGoogleConnectionAsync(setup);
        var packageName = $"com.{setup.Suffix}.android";
        var secondPackage = $"com.{setup.Suffix}.android2";
        _factory.StoreGateway.GoogleApps[packageName] = new StoreAppRecord(packageName, packageName, "Android name");
        _factory.StoreGateway.GoogleApps[secondPackage] = new StoreAppRecord(secondPackage, secondPackage, "Second");

        var (_, added) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = googleConnectionId, packageName });
        var (_, second) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = googleConnectionId, packageName = secondPackage });
        await WaitForJobAsync(second.Data.JobId);

        var appleAppId = (await ListAppsAsync(setup)).Single(a => a.Stores.Any(s => s.Store == StoreKind.Apple)).Id;

        // Act
        var (link, linked) = await setup.Owner.Client.PostAsync<int>("apps/app/link", new { organizationId = setup.OrganizationId, appId = appleAppId, storeAppId = added.Data.StoreAppId });
        var (secondLink, _) = await setup.Owner.Client.PostAsync<int>("apps/app/link", new { organizationId = setup.OrganizationId, appId = appleAppId, storeAppId = second.Data.StoreAppId });
        var (oldApp, _) = await setup.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={setup.OrganizationId}&appId={added.Data.AppId}");
        var (unlink, unlinked) = await setup.Owner.Client.PostAsync<int>("apps/app/unlink", new { organizationId = setup.OrganizationId, storeAppId = added.Data.StoreAppId });
        var (_, splitApp) = await setup.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={setup.OrganizationId}&appId={unlinked.Data}");
        var audited = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().CountAsync(a => a.OrganizationId == setup.OrganizationId && (a.Action == AuditAction.StoreAppLinked || a.Action == AuditAction.StoreAppUnlinked)));

        // Assert
        link.StatusCode.Should().Be(HttpStatusCode.OK);
        linked.Data.Should().Be(appleAppId);
        secondLink.StatusCode.Should().Be(HttpStatusCode.Conflict, "an app has at most one record per store");
        oldApp.StatusCode.Should().Be(HttpStatusCode.NotFound, "the emptied app is removed");
        unlink.StatusCode.Should().Be(HttpStatusCode.OK);
        unlinked.Data.Should().NotBe(appleAppId);
        splitApp.Data.Stores.Should().ContainSingle().Which.Store.Should().Be(StoreKind.Google);
        audited.Should().Be(2);
    }

    [Fact]
    public async Task AppEndpoints_ShouldCheckRolesConnectionsAndOrganizations()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var stranger = await NewSetupAsync();
        var viewerUser = await _factory.CreateUserAsync(email: $"v{TestApi.Unique()}@example.com");
        var viewer = await _factory.LoginAsync(viewerUser.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await setup.Owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = setup.OrganizationId, email = viewerUser.Email, role = OrgRole.Viewer });
        await viewer.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        // Act
        var (wrongStore, _) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = setup.AppleConnectionId, packageName = "com.example.app" });
        var (foreignConnection, _) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = stranger.AppleConnectionId, packageName = "com.example.app" });
        var (badPackage, _) = await setup.Owner.Client.PostAsync<AppAddedDto>("apps/google", new { organizationId = setup.OrganizationId, connectionId = setup.AppleConnectionId, packageName = "not a package" });
        var (viewerSync, _) = await viewer.Client.PostAsync<List<long>>("apps/sync", new { organizationId = setup.OrganizationId });
        var (viewerList, _) = await viewer.Client.PatchListAsync<AppListDto>("apps", new { organizationId = setup.OrganizationId, pageNumber = 1, rowCount = 10 });
        var (strangerList, _) = await stranger.Owner.Client.PatchListAsync<AppListDto>("apps", new { organizationId = setup.OrganizationId, pageNumber = 1, rowCount = 10 });
        var (foreignSync, _) = await setup.Owner.Client.PostAsync<List<long>>("apps/sync", new { organizationId = setup.OrganizationId, connectionId = stranger.AppleConnectionId });

        // Assert
        wrongStore.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreignConnection.StatusCode.Should().Be(HttpStatusCode.NotFound);
        badPackage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        viewerSync.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerList.StatusCode.Should().Be(HttpStatusCode.OK);
        strangerList.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignSync.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sync_ShouldRecordItsSteps()
    {
        var setup = await NewSetupAsync();
        GiveAppleApps(setup, new StoreAppRecord($"5{setup.Suffix}", $"com.{setup.Suffix}.five", "Five"));

        await SyncAsync(setup);

        var steps = await _factory.DbAsync(db => db.StoreJobSteps.AsNoTracking()
                                                   .Where(s => s.StoreJob.OrganizationId == setup.OrganizationId)
                                                   .OrderBy(s => s.Order)
                                                   .Select(s => s.Name)
                                                   .ToListAsync());

        steps.Should().Equal("apps.list", "apps.save", "builds.list", "builds.save");
    }
}
