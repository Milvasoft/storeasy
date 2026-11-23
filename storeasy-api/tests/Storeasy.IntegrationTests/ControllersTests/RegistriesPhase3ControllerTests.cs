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
[Trait("Controller Integration Tests", "Package registries phase 3: downloads and usage with daily snapshots, the live registry listing and its checks, package groups and their suggestions; against fake registries.")]
public class RegistriesPhase3ControllerTests(StoreasyApiFactory factory) : ContentTestBase(factory)
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

    [Fact]
    public async Task Downloads_ShouldKeepTheRegistrysDailyCounts_AndSnapshotsOfTotals()
    {
        // Arrange
        var org = await NewOrgAsync();
        var npm = FakeRegistries.NewName("dl");
        var nuget = "Acme." + FakeRegistries.NewName("Dl").Replace("-", ".", StringComparison.Ordinal);
        var today = DateTime.UtcNow.Date;

        Registries.SetNpm(npm, [new V("1.0.0", _day), new V("1.1.0", _day.AddDays(1))]);
        Registries.SetNpmDownloads(npm, new Dictionary<DateTime, long> { [today.AddDays(-2)] = 40, [today.AddDays(-1)] = 60 }, new Dictionary<string, long> { ["1.1.0"] = 90, ["1.0.0"] = 10 });
        Registries.SetNuGet(nuget, [new V("1.0.0", _day), new V("2.0.0", _day.AddDays(1))]);
        Registries.SetNuGetSearch(nuget, 5000, new Dictionary<string, long> { ["1.0.0"] = 1000, ["2.0.0"] = 4000 });

        // Act
        var npmId = await AddAsync(org, PackageRegistry.Npm, npm);
        var nugetId = await AddAsync(org, PackageRegistry.NuGet, nuget);
        var (npmHttp, npmDownloads) = await org.Owner.Client.GetAsync<PackageDownloadsDto>($"registries/downloads?organizationId={org.OrganizationId}&trackedPackageId={npmId}");
        var (_, nugetDownloads) = await org.Owner.Client.GetAsync<PackageDownloadsDto>($"registries/downloads?organizationId={org.OrganizationId}&trackedPackageId={nugetId}");
        var (_, summary) = await org.Owner.Client.GetAsync<List<TrackedPackageSummaryDto>>($"registries/packages?organizationId={org.OrganizationId}");
        var snapshot = await _factory.DbAsync(db => db.PackageDownloadDays.Where(d => d.TrackedPackageId == nugetId).Select(d => new { d.Day, d.TotalDownloads }).SingleAsync());
        var (otherHttp, _) = await (await NewOrgAsync()).Owner.Client.GetAsync<PackageDownloadsDto>($"registries/downloads?organizationId={org.OrganizationId}&trackedPackageId={npmId}");

        // Assert
        npmHttp.StatusCode.Should().Be(HttpStatusCode.OK, npmDownloads.MessagesToString());
        npmDownloads.Data.Should().BeEquivalentTo(new { RecentDownloads = (long?)100, RecentDownloadsDays = (int?)30, VersionScope = (DownloadScope?)DownloadScope.LastWeek, TotalDownloads = (long?)null, Available = true }, o => o.ExcludingMissingMembers());
        npmDownloads.Data.Series.Select(d => (d.Day.Date, d.Downloads)).Should().Equal((today.AddDays(-2), 40L), (today.AddDays(-1), 60L));
        npmDownloads.Data.Versions.Select(v => (v.Version, v.Downloads)).Should().Equal(("1.1.0", 90L), ("1.0.0", 10L));
        nugetDownloads.Data.TotalDownloads.Should().Be(5000);
        nugetDownloads.Data.VersionScope.Should().Be(DownloadScope.Total);
        nugetDownloads.Data.Versions.Should().HaveCount(2);
        snapshot.Day.Should().Be(today);
        snapshot.TotalDownloads.Should().Be(5000);
        summary.Data.Single(p => p.Id == nugetId).TotalDownloads.Should().Be(5000);
        otherHttp.StatusCode.Should().Be(HttpStatusCode.NotFound, "another organization's package is not found");
    }

    [Fact]
    public async Task Listing_ShouldBeReadLive_WithReadinessChecks()
    {
        // Arrange
        var org = await NewOrgAsync();
        var npm = FakeRegistries.NewName("ls");
        var crate = FakeRegistries.NewName("lc").Replace('-', '_');

        Registries.SetNpm(npm, [new V("1.0.0", _day)]);
        Registries.SetNpmListing(npm, "Tiny", "# Hello\n\nUsage text.", "cli", "tools");
        Registries.SetCrate(crate, [new V("0.1.0", _day)]);
        Registries.SetCrateReadme(crate, "0.1.0", "<h1>Crate</h1><p>Fast &amp; safe.</p><script>alert(1)</script>");
        var npmId = await AddAsync(org, PackageRegistry.Npm, npm);
        var crateId = await AddAsync(org, PackageRegistry.Crates, crate);

        // Act
        var (http, listing) = await org.Owner.Client.GetAsync<PackageListingDto>($"registries/listing?organizationId={org.OrganizationId}&trackedPackageId={npmId}");
        var (_, crateListing) = await org.Owner.Client.GetAsync<PackageListingDto>($"registries/listing?organizationId={org.OrganizationId}&trackedPackageId={crateId}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK, listing.MessagesToString());
        listing.Data.Should().BeEquivalentTo(new { Description = "Tiny", License = "MIT", ShipsWithRelease = true, ReadmeFormat = (ReadmeFormat?)ReadmeFormat.Markdown }, o => o.ExcludingMissingMembers());
        listing.Data.Readme.Should().Contain("Usage text.");
        listing.Data.Keywords.Should().Equal("cli", "tools");
        listing.Data.Checks.Single(c => c.Key == "description").Status.Should().Be(ListingCheckStatus.Warning);
        listing.Data.Checks.Single(c => c.Key == "readme").Status.Should().Be(ListingCheckStatus.Ok);
        crateListing.Data.Readme.Should().Contain("Fast & safe.").And.NotContain("<").And.NotContain("alert");
        crateListing.Data.ReadmeFormat.Should().Be(ReadmeFormat.Html);
    }

    [Fact]
    public async Task Groups_ShouldShowWhoIsBehind_AndBeSuggestedByRepository()
    {
        // Arrange
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        var first = FakeRegistries.NewName("ga");
        var second = FakeRegistries.NewName("gb");

        Registries.SetNpm(first, [new V("11.0.2", _day)]);
        Registries.SetNpm(second, [new V("11.0.1", _day)]);
        var firstId = await AddAsync(org, PackageRegistry.Npm, first);
        var secondId = await AddAsync(org, PackageRegistry.Npm, second);

        // Both packages declare their own repository (acme/<name>), so point them at one to get a suggestion.
        await _factory.DbAsync(db => db.TrackedPackages.Where(p => p.Id == firstId || p.Id == secondId).ExecuteUpdateAsync(s => s.SetProperty(p => p.SourceRepository, "https://github.com/acme/monorepo")));

        // Act
        var (_, suggestions) = await org.Owner.Client.GetAsync<List<PackageGroupSuggestionDto>>($"registries/groups/suggestions?organizationId={org.OrganizationId}");
        var (saveHttp, saved) = await org.Owner.Client.PostAsync<int>("registries/group", new { organizationId = org.OrganizationId, name = "Acme SDKs", trackedPackageIds = new[] { firstId, secondId } });
        var (duplicateHttp, _) = await org.Owner.Client.PostAsync<int>("registries/group", new { organizationId = org.OrganizationId, name = "Acme SDKs", trackedPackageIds = Array.Empty<int>() });
        var (foreignHttp, _) = await other.Owner.Client.PostAsync<int>("registries/group", new { organizationId = other.OrganizationId, name = "Steal", trackedPackageIds = new[] { firstId } });
        var (_, groups) = await org.Owner.Client.GetAsync<List<PackageGroupDto>>($"registries/groups?organizationId={org.OrganizationId}");
        var (otherRemove, _) = await other.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/group", new { organizationId = other.OrganizationId, id = saved.Data });
        var (removeHttp, _) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/group", new { organizationId = org.OrganizationId, id = saved.Data });
        var stillTracked = await _factory.DbAsync(db => db.TrackedPackages.CountAsync(p => (p.Id == firstId || p.Id == secondId) && p.PackageGroupId == null));

        // Assert
        suggestions.Data.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "acme/monorepo", TrackedPackageIds = new List<int> { firstId, secondId } }, o => o.ExcludingMissingMembers());
        saveHttp.StatusCode.Should().Be(HttpStatusCode.OK, saved.MessagesToString());
        duplicateHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        foreignHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var group = groups.Data.Should().ContainSingle().Subject;
        group.HighestVersion.Should().Be("11.0.2");
        group.BehindCount.Should().Be(1);
        group.Members.First().Should().BeEquivalentTo(new { TrackedPackageId = secondId, Behind = true, LatestVersion = "11.0.1" }, o => o.ExcludingMissingMembers());
        otherRemove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        removeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        stillTracked.Should().Be(2);
    }
}
