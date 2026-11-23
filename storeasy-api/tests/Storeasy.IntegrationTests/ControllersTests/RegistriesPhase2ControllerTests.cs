using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using V = Storeasy.IntegrationTests.TestBase.Fakes.FakeRegistries.Version;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Package registries phase 2: the trusted publishing wizard and its publisher check, token expiry reminders without tokens, and Maven Central, RubyGems, Packagist and pub.dev; against fake registries.")]
public class RegistriesPhase2ControllerTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private static readonly DateTime _day = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private FakeRegistries Registries => _factory.Registries;

    private async Task<int> AddAsync(Org org, PackageRegistry registry, string name)
    {
        var (http, added) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry, name });

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

    [Fact]
    public async Task Setup_ShouldGenerateTheWorkflow_CheckTheRelease_AndAlertOnAnotherWorkflow()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("tp");

        Registries.SetNpm(name, [new V("1.0.0", _day, Provenance: true)]);
        var id = await AddAsync(org, PackageRegistry.Npm, name);

        // Act: preview, save, then a release from another workflow.
        var (previewHttp, preview) = await org.Owner.Client.GetAsync<TrustedPublishingDto>($"registries/setup?organizationId={org.OrganizationId}&trackedPackageId={id}&provider={(int)CiProvider.GitHubActions}&repository=acme/{name}&workflow=release.yml&environment=npm");
        var (saveHttp, saved) = await org.Owner.Client.PutAsync<TrustedPublishingDto>("registries/setup", new { organizationId = org.OrganizationId, trackedPackageId = id, provider = CiProvider.GitHubActions, repository = $"acme/{name}", workflow = "release.yml", environment = "npm" });
        var (badHttp, bad) = await org.Owner.Client.PutAsync<TrustedPublishingDto>("registries/setup", new { organizationId = org.OrganizationId, trackedPackageId = id, provider = CiProvider.GitHubActions, repository = "not a repo", workflow = "release.yml" });

        Registries.SetNpm(name, [new V("1.0.0", _day, Provenance: true), new V("1.1.0", _day.AddDays(1), Provenance: true)], workflow: ".github/workflows/manual.yml");
        await SyncAsync(org, id);
        var package = await GetAsync(org, id);
        var (_, setup) = await org.Owner.Client.GetAsync<TrustedPublishingDto>($"registries/setup?organizationId={org.OrganizationId}&trackedPackageId={id}");

        // Assert
        previewHttp.StatusCode.Should().Be(HttpStatusCode.OK, preview.MessagesToString());
        preview.Data.Saved.Should().BeFalse();
        preview.Data.Providers.Should().Equal(CiProvider.GitHubActions, CiProvider.GitLabCi);
        preview.Data.SettingsUrl.Should().Be($"https://www.npmjs.com/package/{name}/access");
        preview.Data.RegistryFields.Select(f => (f.Key, f.Value)).Should().Equal(("owner", "acme"), ("repository", name), ("workflow", "release.yml"), ("environment", "npm"));
        preview.Data.WorkflowPath.Should().Be(".github/workflows/release.yml");
        preview.Data.WorkflowText.Should().Contain("id-token: write").And.Contain("environment: npm");
        preview.Data.Check.Status.Should().Be(PublisherCheckStatus.NotConfigured);

        saveHttp.StatusCode.Should().Be(HttpStatusCode.OK, saved.MessagesToString());
        saved.Data.Saved.Should().BeTrue();
        saved.Data.Check.Status.Should().Be(PublisherCheckStatus.Match);
        badHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        bad.Messages.Should().NotBeEmpty();

        setup.Data.Check.Should().BeEquivalentTo(new { Status = PublisherCheckStatus.Mismatch, ObservedWorkflow = ".github/workflows/manual.yml", Version = "1.1.0" }, o => o.ExcludingMissingMembers());
        package.PublisherCheck.Should().Be(PublisherCheckStatus.Mismatch);
        package.Alerts.Should().ContainSingle(a => a.Kind == PackageAlertKind.PublisherMismatch && a.Version == "1.1.0" && a.ResolvedDate == null);
        (await AuditAsync(org)).Should().Contain([AuditAction.RegistrySetupSaved, AuditAction.RegistryPublisherMismatch]);
    }

    [Fact]
    public async Task Setup_ShouldRefuseProvidersTheRegistryDoesNotOffer()
    {
        // Arrange
        var org = await NewOrgAsync();
        var crate = FakeRegistries.NewName("tc").Replace('-', '_');
        var maven = $"com.acme.{FakeRegistries.NewName("g").Replace("-", string.Empty, StringComparison.Ordinal)}:lib";

        Registries.SetCrate(crate, [new V("1.0.0", _day, Provenance: true)]);
        Registries.SetMaven(maven, [new V("1.0.0", _day)]);
        var crateId = await AddAsync(org, PackageRegistry.Crates, crate);
        var mavenId = await AddAsync(org, PackageRegistry.Maven, maven);

        // Act
        var (gitlabHttp, gitlab) = await org.Owner.Client.PutAsync<TrustedPublishingDto>("registries/setup", new { organizationId = org.OrganizationId, trackedPackageId = crateId, provider = CiProvider.GitLabCi, repository = "acme/lib", workflow = ".gitlab-ci.yml" });
        var (_, mavenSetup) = await org.Owner.Client.GetAsync<TrustedPublishingDto>($"registries/setup?organizationId={org.OrganizationId}&trackedPackageId={mavenId}");

        // Assert
        gitlabHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gitlab.Messages.Should().Contain(m => m.Key == "RegistryTrustedPublishingUnsupported");
        mavenSetup.Data.Providers.Should().BeEmpty();
        mavenSetup.Data.SettingsUrl.Should().BeNull();
    }

    [Fact]
    public async Task Reminders_ShouldRefuseTokens_AndWarnBeforeTheExpiry()
    {
        // Arrange
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        var soon = DateTime.UtcNow.Date.AddDays(2);

        // Act
        var (tokenHttp, token) = await org.Owner.Client.PostAsync<int>("registries/reminder", new { organizationId = org.OrganizationId, registry = PackageRegistry.Npm, label = "npm_A1b2C3d4E5f6G7h8I9j0K1l2M3", expiryDate = soon });
        var (addHttp, added) = await org.Owner.Client.PostAsync<int>("registries/reminder", new { organizationId = org.OrganizationId, registry = PackageRegistry.NuGet, label = "NuGet push key", usedIn = "NUGET_API_KEY in acme/lib", expiryDate = soon });
        var (_, reminders) = await org.Owner.Client.GetAsync<List<TokenReminderDto>>($"registries/reminders?organizationId={org.OrganizationId}");
        var (_, alerts) = await org.Owner.Client.GetAsync<List<PackageAlertDto>>($"registries/alerts?organizationId={org.OrganizationId}");
        var notified = await _factory.DbAsync(db => db.InternalNotifications.CountAsync(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.PackageRegistry));
        var (againHttp, _) = await org.Owner.Client.PostAsync<int>("registries/reminder", new { organizationId = org.OrganizationId, id = added.Data, registry = PackageRegistry.NuGet, label = "NuGet push key", expiryDate = soon });
        var notifiedAgain = await _factory.DbAsync(db => db.InternalNotifications.CountAsync(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.PackageRegistry));
        var (movedHttp, _) = await org.Owner.Client.PostAsync<int>("registries/reminder", new { organizationId = org.OrganizationId, id = added.Data, registry = PackageRegistry.NuGet, label = "NuGet push key", expiryDate = soon.AddDays(200) });
        var (_, alertsAfter) = await org.Owner.Client.GetAsync<List<PackageAlertDto>>($"registries/alerts?organizationId={org.OrganizationId}");
        var (otherHttp, _) = await other.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/reminder", new { organizationId = other.OrganizationId, id = added.Data });
        var (removeHttp, _) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/reminder", new { organizationId = org.OrganizationId, id = added.Data });

        // Assert
        tokenHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        token.Messages.Should().Contain(m => m.Message.Contains("token", StringComparison.OrdinalIgnoreCase));
        addHttp.StatusCode.Should().Be(HttpStatusCode.OK, added.MessagesToString());
        reminders.Data.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Label = "NuGet push key", DaysLeft = 2, Stage = (byte)2, TrustedPublishingAvailable = true }, o => o.ExcludingMissingMembers());
        alerts.Data.Should().ContainSingle(a => a.Kind == PackageAlertKind.TokenExpiring && a.TokenReminderId == added.Data && a.PackageName == "NuGet push key" && a.Registry == PackageRegistry.NuGet);
        notified.Should().Be(1);
        againHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        notifiedAgain.Should().Be(1, "a stage is warned once");
        movedHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        alertsAfter.Data.Should().NotContain(a => a.Kind == PackageAlertKind.TokenExpiring);
        otherHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        removeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuditAsync(org)).Should().Contain([AuditAction.RegistryTokenReminderSaved, AuditAction.RegistryTokenExpiring, AuditAction.RegistryTokenReminderRemoved]);
    }

    [Fact]
    public async Task NewRegistries_ShouldBeReadWithoutProvenanceAlertsWhereThereIsNone()
    {
        // Arrange
        var org = await NewOrgAsync();
        var maven = $"com.acme.{FakeRegistries.NewName("m").Replace("-", string.Empty, StringComparison.Ordinal)}:core";
        var gem = FakeRegistries.NewName("gem");
        var php = $"acme/{FakeRegistries.NewName("php")}";
        var dart = FakeRegistries.NewName("dart").Replace('-', '_');

        Registries.SetMaven(maven, [new V("1.0.0", _day), new V("1.1.0", _day.AddDays(1))]);
        Registries.SetRubyGem(gem, [new V("2.0.0", _day), new V("2.1.0", _day.AddDays(1), Provenance: true)]);
        Registries.SetPackagist(php, [new V("3.0.0", _day), new V("3.1.0", _day.AddDays(1))], abandoned: "acme/next");
        Registries.SetPub(dart, [new V("0.9.0", _day), new V("1.0.0", _day.AddDays(1))], replacedBy: "dart_next");

        // Act
        var mavenPackage = await GetAsync(org, await AddAsync(org, PackageRegistry.Maven, maven));
        var gemPackage = await GetAsync(org, await AddAsync(org, PackageRegistry.RubyGems, gem));
        var phpPackage = await GetAsync(org, await AddAsync(org, PackageRegistry.Packagist, php));
        var dartPackage = await GetAsync(org, await AddAsync(org, PackageRegistry.Pub, dart));

        // Assert
        mavenPackage.Should().BeEquivalentTo(new { LatestVersion = "1.1.0", LatestPublishedDate = (DateTime?)_day.AddDays(1), ProvenanceSupported = false }, o => o.ExcludingMissingMembers());
        mavenPackage.Alerts.Should().BeEmpty();
        gemPackage.Should().BeEquivalentTo(new { LatestVersion = "2.1.0", HasProvenance = true, LatestCiRunUrl = $"https://github.com/acme/{gem}/actions/runs/31" }, o => o.ExcludingMissingMembers());
        phpPackage.DeprecationMessage.Should().Be("Abandoned; use acme/next");
        phpPackage.Alerts.Select(a => a.Kind).Should().Equal(PackageAlertKind.Deprecated);
        dartPackage.DeprecationMessage.Should().Be("Discontinued; use dart_next");
        dartPackage.SourceRepository.Should().Be($"https://github.com/acme/{dart}");
        dartPackage.Alerts.Select(a => a.Kind).Should().Equal(PackageAlertKind.Deprecated);
    }
}
