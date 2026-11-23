using FluentAssertions;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Registries;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Package registries phase 3: download answers, daily snapshots and series, registry listings and their checks, groups.")]
public class RegistryPhase3Tests
{
    private static readonly DateTime _today = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    #region Downloads

    [Fact]
    public void NuGetSearch_ShouldReadTotalsAndVersions()
    {
        const string json = """{ "totalHits": 1, "data": [ { "id": "Newtonsoft.Json", "totalDownloads": 9930560080, "versions": [ { "version": "13.0.3", "downloads": 100, "@id": "x" }, { "version": "13.0.4", "downloads": 50 } ] } ] }""";

        var read = RegistryDownloads.ParseNuGetSearch(json, "newtonsoft.json");

        read.Total.Should().Be(9930560080);
        read.Versions.Should().BeEquivalentTo(new Dictionary<string, long> { ["13.0.3"] = 100, ["13.0.4"] = 50 });
        read.VersionScope.Should().Be(DownloadScope.Total);
        RegistryDownloads.ParseNuGetSearch("""{ "data": [] }""", "x").Total.Should().BeNull();
    }

    [Fact]
    public void Npm_ShouldReadDaysAndLastWeekPerVersion()
    {
        const string range = """{ "start": "2026-11-20", "end": "2026-11-22", "package": "left-pad", "downloads": [ { "downloads": 10, "day": "2026-11-20" }, { "downloads": 20, "day": "2026-11-22" } ] }""";
        const string versions = """{ "package": "left-pad", "downloads": { "1.3.0": 7, "1.3.0.tgz?v=-1.3.1": 1 } }""";

        var read = RegistryDownloads.ParseNpm(range, versions, _today);

        read.Days.Should().HaveCount(2);
        read.Days[new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc)].Should().Be(20);
        read.Recent.Should().Be(30);
        read.Versions["1.3.0"].Should().Be(7);
        read.VersionScope.Should().Be(DownloadScope.LastWeek);
    }

    [Fact]
    public void PyPICrateRubyGemsPackagistPub_ShouldReadTheirCounts()
    {
        var pypi = RegistryDownloads.ParsePyPIStats("""{ "data": { "last_day": 1, "last_month": 300, "last_week": 70 } }""",
                                                    """{ "data": [ { "category": "with_mirrors", "date": "2026-11-21", "downloads": 99 }, { "category": "without_mirrors", "date": "2026-11-21", "downloads": 9 } ] }""");
        var crate = RegistryDownloads.ParseCrate("""{ "crate": { "downloads": 1000, "recent_downloads": 90 }, "versions": [ { "id": 7, "num": "1.0.0", "downloads": 600 } ] }""",
                                                 """{ "version_downloads": [ { "version": 7, "downloads": 5, "date": "2026-11-20" }, { "version": 8, "downloads": 3, "date": "2026-11-20" } ], "meta": { "extra_downloads": [ { "date": "2026-11-20", "downloads": 2 } ] } }""");
        var gem = RegistryDownloads.ParseRubyGems("""{ "downloads": 500 }""", """[ { "number": "1.0.0", "downloads_count": 100, "platform": "ruby" }, { "number": "1.0.0", "downloads_count": 20, "platform": "java" } ]""");
        var packagist = RegistryDownloads.ParsePackagist("""{ "package": { "downloads": { "total": 80, "monthly": 8, "daily": 1 }, "favers": 4 } }""");
        var pub = RegistryDownloads.ParsePubScore("""{ "grantedPoints": 150, "maxPoints": 160, "likeCount": 42, "downloadCount30Days": 1234, "tags": [] }""");

        pypi.Should().BeEquivalentTo(new { Recent = (long?)300, Total = (long?)null }, o => o.ExcludingMissingMembers());
        pypi.Days.Values.Should().Equal(9L);
        crate.Total.Should().Be(1000);
        crate.RecentDays.Should().Be(90);
        crate.Days.Values.Should().Equal(10L);
        crate.Versions["1.0.0"].Should().Be(600);
        gem.Total.Should().Be(500);
        gem.Versions["1.0.0"].Should().Be(120);
        packagist.Should().BeEquivalentTo(new { Total = (long?)80, Recent = (long?)8, Likes = (long?)4 }, o => o.ExcludingMissingMembers());
        pub.Should().BeEquivalentTo(new { Recent = (long?)1234, Likes = (long?)42, Points = (int?)150, MaxPoints = (int?)160 }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void Apply_ShouldSnapshotTodaysTotal_AndUpdateChangedRowsOnly()
    {
        var package = new TrackedPackage { Id = 3, OrganizationId = 9 };
        var known = new TrackedPackageVersion { Id = 11, Version = "1.0.0", Downloads = 5 };
        var added = new TrackedPackageVersion { Version = "1.1.0" };
        var yesterday = new PackageDownloadDay { Id = 1, TrackedPackageId = 3, Day = _today.AddDays(-1), TotalDownloads = 900 };
        var read = new RegistryDownloadsRead { Total = 1000, VersionScope = DownloadScope.Total };

        read.Versions["1.0.0"] = 7;
        read.Versions["1.1.0"] = 3;

        var outcome = RegistryDownloads.Apply(package, read, [known, added], [yesterday], _today.AddHours(6));

        package.TotalDownloads.Should().Be(1000);
        package.VersionDownloadsScope.Should().Be(DownloadScope.Total);
        outcome.UpdatedVersions.Should().ContainSingle().Which.Should().BeSameAs(known);
        added.Downloads.Should().Be(3);
        outcome.AddedDays.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Day = _today, TotalDownloads = (long?)1000, Downloads = (long?)null, OrganizationId = 9 }, o => o.ExcludingMissingMembers());
        outcome.UpdatedDays.Should().BeEmpty();
        RegistryDownloads.Series([yesterday, outcome.AddedDays[0]]).Should().Equal((_today, 100L));
    }

    #endregion

    #region Listing

    [Fact]
    public void Listing_ShouldReadEachRegistry_AndCheckIt()
    {
        var npm = RegistryListing.ParseNpm("""{ "dist-tags": { "latest": "1.3.0" }, "description": "String left pad", "readme": "## left-pad", "keywords": ["pad"], "license": "WTFPL", "repository": { "url": "git+https://github.com/a/b.git" }, "author": { "name": "Ann" }, "maintainers": [ { "name": "Ann" }, { "name": "Bo" } ] }""");
        var pypi = RegistryListing.ParsePyPI("""{ "info": { "version": "2.0", "summary": "Python HTTP for Humans.", "description": "Requests\n========", "description_content_type": "text/x-rst", "keywords": null, "license_expression": "Apache-2.0", "project_urls": { "Source": "https://github.com/psf/requests" } } }""");
        var nuget = RegistryListing.ParseNuGet("""{ "data": [ { "id": "Acme.Lib", "version": "2.0.0", "description": "Lib", "tags": [""], "projectUrl": "https://github.com/acme/lib", "authors": ["Acme"] } ] }""", "acme.lib", null);
        var gem = RegistryListing.ParseRubyGems("""{ "version": "13.4.2", "info": "Rake is a Make-like program implemented in Ruby", "licenses": ["MIT"], "authors": "Hiroshi SHIBATA, Eric Hodel", "source_code_uri": "https://github.com/ruby/rake" }""");

        npm.Authors.Should().Equal("Ann", "Bo");
        npm.Repository.Should().Be("https://github.com/a/b");
        npm.ReadmeFormat.Should().Be(ReadmeFormat.Markdown);
        pypi.ReadmeFormat.Should().Be(ReadmeFormat.Text);
        pypi.Repository.Should().Be("https://github.com/psf/requests");
        gem.Authors.Should().Equal("Hiroshi SHIBATA", "Eric Hodel");
        RegistryListing.Checks(PackageRegistry.NuGet, nuget).Select(c => (c.Key, c.Status)).Should().Equal(
            ("description", ListingCheckStatus.Warning), ("readme", ListingCheckStatus.Missing), ("license", ListingCheckStatus.Missing),
            ("repository", ListingCheckStatus.Ok), ("keywords", ListingCheckStatus.Warning), ("icon", ListingCheckStatus.Warning));
        RegistryListing.Checks(PackageRegistry.RubyGems, gem).Should().NotContain(c => c.Key == "readme" || c.Key == "icon");
        RegistryListing.ShipsWithRelease(PackageRegistry.Packagist).Should().BeFalse();
    }

    [Fact]
    public void HtmlToText_ShouldDropMarkupAndScripts()
        => RegistryListing.HtmlToText("<h1>Title</h1><script>evil()</script><p>A &amp; B<br>C</p>").Should().Be("Title\nA & B\nC");

    #endregion

    #region Groups

    [Theory]
    [InlineData("11.0.2", "11.0.1", 1)]
    [InlineData("11.0.10", "11.0.9", 1)]
    [InlineData("1.0.0-rc.1", "1.0.0", -1)]
    [InlineData("1.0", "1.0.0", 0)]
    [InlineData("2.0.0+build.5", "2.0.0", 0)]
    public void Compare_ShouldOrderVersions(string left, string right, int sign) => Math.Sign(PackageGroups.Compare(left, right)).Should().Be(sign);

    [Fact]
    public void Suggest_ShouldGroupPackagesOfOneRepository()
    {
        var packages = new[]
        {
            new TrackedPackage { Id = 1, SourceRepository = "https://github.com/Milvasoft/Milvasoft" },
            new TrackedPackage { Id = 2, SourceRepository = "https://github.com/milvasoft/milvasoft.git" },
            new TrackedPackage { Id = 3, SourceRepository = "https://github.com/Milvasoft/milvaion/tree/master/sdks/node" },
            new TrackedPackage { Id = 4, SourceRepository = "https://github.com/Milvasoft/milvaion" },
            new TrackedPackage { Id = 5, SourceRepository = "https://github.com/Milvasoft/pidief" },
            new TrackedPackage { Id = 6, SourceRepository = "https://github.com/Milvasoft/Milvasoft", PackageGroupId = 1 },
        };

        var suggestions = PackageGroups.Suggest(packages);

        suggestions.Select(s => s.TrackedPackageIds).Should().BeEquivalentTo([new List<int> { 1, 2 }, new List<int> { 3, 4 }]);
        PackageGroups.Highest(["1.2.0", "1.10.0", null, "1.9.9"]).Should().Be("1.10.0");
    }

    #endregion
}
