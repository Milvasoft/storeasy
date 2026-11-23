using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using V = Storeasy.IntegrationTests.TestBase.Fakes.FakeRegistries.Version;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Package registries: tracking npm, NuGet, PyPI and crates.io packages, versions, provenance, alerts, audit and notifications, organization isolation and the feature flag; against fake registries and OSV.dev.")]
public class RegistriesControllerTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private static readonly DateTime _day = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private FakeRegistries Registries => _factory.Registries;

    private async Task<int> AddAsync(Org org, PackageRegistry registry, string name, int? appId = null)
    {
        var (http, added) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry, name, appId });

        http.StatusCode.Should().Be(HttpStatusCode.OK, added.MessagesToString());
        (await WaitForJobAsync(added.Data.JobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        return added.Data.Id;
    }

    private async Task SyncAsync(Org org, int packageId)
    {
        var (http, job) = await org.Owner.Client.PostAsync<long>("registries/sync", new { organizationId = org.OrganizationId, trackedPackageId = packageId });

        http.StatusCode.Should().Be(HttpStatusCode.OK, job.MessagesToString());
        (await WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    private async Task<TrackedPackageDto> GetAsync(Org org, int packageId)
    {
        var (http, package) = await org.Owner.Client.GetAsync<TrackedPackageDto>($"registries/package?organizationId={org.OrganizationId}&trackedPackageId={packageId}");

        http.StatusCode.Should().Be(HttpStatusCode.OK, package.MessagesToString());

        return package.Data;
    }

    private Task<List<AuditAction>> AuditAsync(Org org)
        => _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());

    private Task<int> NotificationsAsync(Org org)
        => _factory.DbAsync(db => db.InternalNotifications.CountAsync(n => n.OrganizationId == org.OrganizationId && n.RecipientUserId == org.Owner.Id && n.Type == NotificationType.PackageRegistry));

    [Fact]
    public async Task Npm_ShouldShowTheLatestVersionAndItsCiRun_AndAlertOnChanges()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("npm");

        Registries.SetNpm(name, [new V("1.0.0", _day, Provenance: true), new V("1.1.0", _day.AddDays(1), Provenance: true)], runId: 4242);

        // Act: add, then a release without provenance and a deprecated old version.
        var id = await AddAsync(org, PackageRegistry.Npm, name);
        var first = await GetAsync(org, id);
        var (_, summary) = await org.Owner.Client.GetAsync<List<TrackedPackageSummaryDto>>($"registries/packages?organizationId={org.OrganizationId}");

        Registries.SetNpm(name, [new V("1.0.0", _day, Provenance: true, Deprecated: "Use 1.1"), new V("1.1.0", _day.AddDays(1), Provenance: true), new V("1.2.0", _day.AddDays(2))]);
        await SyncAsync(org, id);
        var second = await GetAsync(org, id);
        var notifications = await NotificationsAsync(org);

        Registries.SetNpm(name, [new V("1.0.0", _day, Provenance: true, Deprecated: "Use 1.1"), new V("1.1.0", _day.AddDays(1), Provenance: true), new V("1.2.0", _day.AddDays(2)), new V("1.3.0", _day.AddDays(3), Provenance: true)], runId: 4343);
        await SyncAsync(org, id);
        var third = await GetAsync(org, id);
        var audit = await AuditAsync(org);

        // Assert
        first.LatestVersion.Should().Be("1.1.0");
        first.LatestPublishedDate.Should().Be(_day.AddDays(1));
        first.HasProvenance.Should().BeTrue();
        first.LatestCiRunUrl.Should().Be($"https://github.com/acme/{name}/actions/runs/4242");
        first.LatestWorkflow.Should().Be(".github/workflows/release.yml");
        first.SourceRepository.Should().Be($"https://github.com/acme/{name}");
        first.Versions.Select(v => v.Version).Should().Equal("1.1.0", "1.0.0");
        first.Alerts.Should().BeEmpty();
        first.RegistryUrl.Should().Be($"https://www.npmjs.com/package/{name}");
        summary.Data.Should().ContainSingle(p => p.Id == id).Which.OpenAlertCount.Should().Be(0);

        second.LatestVersion.Should().Be("1.2.0");
        second.HasProvenance.Should().BeFalse();
        second.LatestCiRunUrl.Should().BeNull();
        second.Alerts.Where(a => a.ResolvedDate == null).Select(a => (a.Kind, a.Version)).Should().BeEquivalentTo([(PackageAlertKind.NoProvenance, "1.2.0"), (PackageAlertKind.Deprecated, "1.0.0")]);
        second.OpenAlertCount.Should().Be(2);
        second.Versions.Single(v => v.Version == "1.0.0").IsDeprecated.Should().BeTrue();
        notifications.Should().Be(2, "the new version and the deprecation are notified");

        third.LatestCiRunUrl.Should().Be($"https://github.com/acme/{name}/actions/runs/4343");
        third.Alerts.Single(a => a.Kind == PackageAlertKind.NoProvenance).ResolvedDate.Should().NotBeNull();
        third.Alerts.Single(a => a.Kind == PackageAlertKind.Deprecated).ResolvedDate.Should().BeNull("the old version stays deprecated");
        audit.Should().Contain([AuditAction.RegistryPackageAdded, AuditAction.RegistryVersionPublished, AuditAction.RegistryVersionDeprecated]);
        audit.Count(a => a == AuditAction.RegistryVersionPublished).Should().Be(2);
        Registries.UserAgents.Should().Contain(ua => ua.Contains("storeasy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Npm_ScopedPackage_ShouldBeReadWithItsEncodedName()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = $"@acme/{FakeRegistries.NewName("lib")}";

        Registries.SetNpm(name, [new V("2.0.0", _day, Provenance: true)]);

        // Act
        var package = await GetAsync(org, await AddAsync(org, PackageRegistry.Npm, name));

        // Assert
        package.Name.Should().Be(name);
        package.HasProvenance.Should().BeTrue();
        package.LatestCiRunUrl.Should().EndWith("/actions/runs/1001");
    }

    [Fact]
    public async Task NuGet_ShouldReadPagedRegistrations_AndNeverAlertForMissingProvenance()
    {
        // Arrange
        var org = await NewOrgAsync();
        var id = "Acme." + FakeRegistries.NewName("Tools").Replace("-", ".", StringComparison.Ordinal);

        Registries.SetNuGet(id, [new V("1.0.0", _day), new V("2.0.0", _day.AddDays(1)), new V("3.0.0-beta.1", _day.AddDays(2))], pagedOut: true);

        // Act: add with a lower case id, then the latest is deprecated and an old version unlisted.
        var packageId = await AddAsync(org, PackageRegistry.NuGet, id.ToLowerInvariant());
        var first = await GetAsync(org, packageId);

        Registries.SetNuGet(id, [new V("1.0.0", _day, Yanked: true), new V("2.0.0", _day.AddDays(1), Deprecated: "Moved to Acme.Next"), new V("3.0.0-beta.1", _day.AddDays(2))], pagedOut: true);
        await SyncAsync(org, packageId);
        var second = await GetAsync(org, packageId);

        // Assert
        first.Name.Should().Be(id, "the registry's spelling is kept");
        first.LatestVersion.Should().Be("2.0.0", "the highest listed stable version");
        first.ProvenanceSupported.Should().BeFalse();
        first.Alerts.Should().BeEmpty();
        second.DeprecationMessage.Should().Be("Moved to Acme.Next");
        second.Alerts.Select(a => (a.Kind, a.Version)).Should().BeEquivalentTo([(PackageAlertKind.Deprecated, "2.0.0"), (PackageAlertKind.Yanked, "1.0.0")]);
        second.Versions.Single(v => v.Version == "1.0.0").IsUnlisted.Should().BeTrue();
        (await AuditAsync(org)).Should().Contain([AuditAction.RegistryVersionDeprecated, AuditAction.RegistryVersionYanked]);
    }

    [Fact]
    public async Task PyPI_ShouldReadProvenanceFromTheIntegrityApi_AndAlertOnAYank()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("py");

        Registries.SetPyPI(name, [new V("0.9.0", _day), new V("1.0.0", _day.AddDays(1), Provenance: true)]);

        // Act
        var packageId = await AddAsync(org, PackageRegistry.PyPI, name);
        var first = await GetAsync(org, packageId);

        Registries.SetPyPI(name, [new V("0.9.0", _day, Yanked: true), new V("1.0.0", _day.AddDays(1), Provenance: true)]);
        await SyncAsync(org, packageId);
        var second = await GetAsync(org, packageId);

        // Assert
        first.LatestVersion.Should().Be("1.0.0");
        first.HasProvenance.Should().BeTrue();
        first.LatestWorkflow.Should().Be("release.yml");
        first.LatestCiRunUrl.Should().BeNull("PyPI names the workflow, not the run");
        first.SourceRepository.Should().Be($"https://github.com/acme/{name}");
        first.Versions.Single(v => v.Version == "0.9.0").HasProvenance.Should().BeFalse("older versions are checked a few per sync");
        second.Alerts.Should().ContainSingle(a => a.Kind == PackageAlertKind.Yanked && a.Version == "0.9.0" && a.Summary == "Broken build");
    }

    [Fact]
    public async Task Crates_ShouldShowTheTrustedPublishingRun_AndTrackAdvisories()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("crate").Replace('-', '_');
        var advisory = $"RUSTSEC-2026-{Random.Shared.Next(1000, 9999)}";

        Registries.SetCrate(name, [new V("0.1.0", _day), new V("0.2.0", _day.AddDays(1), Provenance: true)], runId: 777);
        Registries.SetAdvisory("crates.io", name, "0.2.0", advisory, "CRITICAL", "Memory corruption");

        // Act: an advisory, OSV.dev down (the alert stays), then the advisory is gone.
        var packageId = await AddAsync(org, PackageRegistry.Crates, name);
        var first = await GetAsync(org, packageId);
        var notified = await NotificationsAsync(org);

        Registries.OsvDown = true;

        try
        {
            await SyncAsync(org, packageId);
        }
        finally
        {
            Registries.OsvDown = false;
        }

        var whileDown = await GetAsync(org, packageId);

        Registries.ClearAdvisories("crates.io", name, "0.2.0");
        await SyncAsync(org, packageId);
        var fixedPackage = await GetAsync(org, packageId);
        var (_, alerts) = await org.Owner.Client.GetAsync<List<PackageAlertDto>>($"registries/alerts?organizationId={org.OrganizationId}&includeResolved=true");

        // Assert
        first.LatestCiRunUrl.Should().Be($"https://github.com/acme/{name}/actions/runs/777");
        first.HasProvenance.Should().BeTrue();
        first.Alerts.Should().ContainSingle(a => a.Kind == PackageAlertKind.Advisory)
             .Which.Should().BeEquivalentTo(new { AdvisoryId = advisory, Severity = "CRITICAL", Summary = "Memory corruption", Version = "0.2.0" }, o => o.ExcludingMissingMembers());
        notified.Should().Be(1, "a new advisory is notified on the first sync too");
        whileDown.Alerts.Single(a => a.Kind == PackageAlertKind.Advisory).ResolvedDate.Should().BeNull();
        fixedPackage.Alerts.Single(a => a.Kind == PackageAlertKind.Advisory).ResolvedDate.Should().NotBeNull();
        alerts.Data.Should().Contain(a => a.AdvisoryId == advisory && a.PackageName == name && a.Registry == PackageRegistry.Crates);
        (await AuditAsync(org)).Should().Contain(AuditAction.RegistryAdvisoryFound);
    }

    [Fact]
    public async Task Add_ShouldCheckTheNameAndTheRegistry()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("dup");

        Registries.SetNpm(name, [new V("1.0.0", _day)]);
        await AddAsync(org, PackageRegistry.Npm, name);

        // Act
        var (invalidHttp, _) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry = PackageRegistry.Npm, name = "Has Spaces" });
        var (missingHttp, missing) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry = PackageRegistry.Npm, name = FakeRegistries.NewName("none") });
        var (duplicateHttp, _) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry = PackageRegistry.Npm, name });
        var (appHttp, _) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry = PackageRegistry.Npm, name = FakeRegistries.NewName("app"), appId = int.MaxValue });

        // Assert
        invalidHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.Messages.Should().Contain(m => m.Key == "RegistryPackageNotFound");
        duplicateHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        appHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnotherOrganizationsPackage_ShouldBeNotFound_AndViewersShouldOnlyRead()
    {
        // Arrange
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        var name = FakeRegistries.NewName("iso");

        Registries.SetNpm(name, [new V("1.0.0", _day)]);
        var packageId = await AddAsync(org, PackageRegistry.Npm, name);

        // Act
        var (otherGet, _) = await other.Owner.Client.GetAsync<TrackedPackageDto>($"registries/package?organizationId={other.OrganizationId}&trackedPackageId={packageId}");
        var (otherSync, _) = await other.Owner.Client.PostAsync<long>("registries/sync", new { organizationId = other.OrganizationId, trackedPackageId = packageId });
        var (otherRemove, _) = await other.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/package", new { organizationId = other.OrganizationId, trackedPackageId = packageId });
        var (_, otherList) = await other.Owner.Client.GetAsync<List<TrackedPackageSummaryDto>>($"registries/packages?organizationId={other.OrganizationId}");
        var (viewerGet, _) = await viewer.Client.GetAsync<TrackedPackageDto>($"registries/package?organizationId={org.OrganizationId}&trackedPackageId={packageId}");
        var (viewerSync, _) = await viewer.Client.PostAsync<long>("registries/sync", new { organizationId = org.OrganizationId, trackedPackageId = packageId });
        var (viewerRemove, _) = await viewer.Client.SendAsync<int>(HttpMethod.Delete, "registries/package", new { organizationId = org.OrganizationId, trackedPackageId = packageId });
        var (ownerRemove, removed) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/package", new { organizationId = org.OrganizationId, trackedPackageId = packageId });
        var remaining = await _factory.DbAsync(db => db.TrackedPackageVersions.CountAsync(v => v.TrackedPackageId == packageId));

        // Assert
        otherGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherSync.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherRemove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherList.Data.Should().BeEmpty();
        viewerGet.StatusCode.Should().Be(HttpStatusCode.OK);
        viewerSync.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerRemove.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ownerRemove.StatusCode.Should().Be(HttpStatusCode.OK, removed.MessagesToString());
        remaining.Should().Be(0);
        (await AuditAsync(org)).Should().Contain(AuditAction.RegistryPackageRemoved);
    }

    [Fact]
    public async Task SwitchedOffModule_ShouldAnswer503_AndQueueNoScheduledSync()
    {
        // Arrange
        var org = await NewOrgAsync();
        var flagId = await _factory.DbAsync(db => db.FeatureFlags.Where(f => f.Key == FeatureFlagKeys.Registries).Select(f => f.Id).SingleAsync());

        await _factory.DbAsync(db => db.FeatureFlags.Where(f => f.Id == flagId).ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, false)));
        await _factory.FlushRedisAsync();

        try
        {
            // Act
            var (http, _) = await org.Owner.Client.GetAsync<List<TrackedPackageSummaryDto>>($"registries/packages?organizationId={org.OrganizationId}");
            var queued = await _factory.WithServiceAsync<Storeasy.Application.Interfaces.IRegistrySyncer, int>(syncer => syncer.QueueDueAsync(CancellationToken.None));

            // Assert
            http.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            queued.Should().Be(0);
        }
        finally
        {
            await _factory.DbAsync(db => db.FeatureFlags.Where(f => f.Id == flagId).ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, true)));
            await _factory.FlushRedisAsync();
        }
    }
}
