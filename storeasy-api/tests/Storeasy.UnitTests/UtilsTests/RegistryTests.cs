using FluentAssertions;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Registries;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Package registries: names, the npm, NuGet, PyPI, crates.io and OSV.dev answers, provenance and the alert rules.")]
public class RegistryTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    #region Names

    [Theory]
    [InlineData(PackageRegistry.Npm, "left-pad", true)]
    [InlineData(PackageRegistry.Npm, "@sigstore/bundle", true)]
    [InlineData(PackageRegistry.Npm, "Left-Pad", false)]
    [InlineData(PackageRegistry.Npm, ".hidden", false)]
    [InlineData(PackageRegistry.Npm, "@scope/", false)]
    [InlineData(PackageRegistry.NuGet, "Newtonsoft.Json", true)]
    [InlineData(PackageRegistry.NuGet, "My_Lib-Core.2", true)]
    [InlineData(PackageRegistry.NuGet, "bad..id", false)]
    [InlineData(PackageRegistry.PyPI, "Django", true)]
    [InlineData(PackageRegistry.PyPI, "zope.interface", true)]
    [InlineData(PackageRegistry.PyPI, "-bad", false)]
    [InlineData(PackageRegistry.Crates, "serde_json", true)]
    [InlineData(PackageRegistry.Crates, "tokio-util", true)]
    [InlineData(PackageRegistry.Crates, "1crate", false)]
    [InlineData(PackageRegistry.Crates, "", false)]
    public void Name_ShouldFollowTheRegistrysRules(PackageRegistry registry, string name, bool valid)
        => RegistryNames.IsValid(registry, name).Should().Be(valid);

    [Fact]
    public void PathName_ShouldEncodeScopesAndNormalizeNames()
    {
        RegistryNames.PathName(PackageRegistry.Npm, "@sigstore/bundle").Should().Be("@sigstore%2fbundle");
        RegistryNames.PathName(PackageRegistry.Npm, "is-odd").Should().Be("is-odd");
        RegistryNames.PathName(PackageRegistry.NuGet, "Newtonsoft.Json").Should().Be("newtonsoft.json");
        RegistryNames.PathName(PackageRegistry.PyPI, "Zope_Interface..Extra").Should().Be("zope-interface-extra");
        RegistryNames.OsvEcosystem(PackageRegistry.Crates).Should().Be("crates.io");
        RegistryNames.OsvEcosystem(PackageRegistry.NuGet).Should().Be("NuGet");
        RegistryNames.SupportsProvenance(PackageRegistry.NuGet).Should().BeFalse();
        RegistryNames.PageUrl(PackageRegistry.Crates, "serde").Should().Be("https://crates.io/crates/serde");
    }

    #endregion

    #region npm

    private const string NpmPackument = """
        {
          "_id": "left-pad", "name": "left-pad",
          "dist-tags": { "latest": "1.3.0" },
          "repository": { "type": "git", "url": "git+https://github.com/stevemao/left-pad.git" },
          "time": { "created": "2014-03-18T00:00:00.000Z", "modified": "2026-01-01T00:00:00.000Z", "1.2.0": "2017-11-01T10:00:00.000Z", "1.3.0": "2018-04-09T01:30:00.000Z" },
          "versions": {
            "1.2.0": { "name": "left-pad", "version": "1.2.0", "dist": { "tarball": "x", "shasum": "y" } },
            "1.3.0": { "name": "left-pad", "version": "1.3.0", "deprecated": "use String.prototype.padStart()",
                       "dist": { "tarball": "x", "shasum": "y", "attestations": { "url": "https://registry.npmjs.org/-/npm/v1/attestations/left-pad@1.3.0", "provenance": { "predicateType": "https://slsa.dev/provenance/v1" } } } }
          }
        }
        """;

    [Fact]
    public void Npm_ShouldReadVersionsDatesDeprecationAndProvenanceFlags()
    {
        var read = RegistryParsers.ParseNpm(NpmPackument);

        read.Name.Should().Be("left-pad");
        read.LatestVersion.Should().Be("1.3.0");
        read.Repository.Should().Be("https://github.com/stevemao/left-pad");
        read.Versions.Should().HaveCount(2);
        read.Latest.Published.Should().Be(new DateTime(2018, 4, 9, 1, 30, 0, DateTimeKind.Utc));
        read.Latest.Deprecated.Should().BeTrue();
        read.Latest.Message.Should().Be("use String.prototype.padStart()");
        read.Latest.HasProvenance.Should().BeTrue();
        read.Versions.Single(v => v.Version == "1.2.0").HasProvenance.Should().BeFalse();
        read.Versions.Single(v => v.Version == "1.2.0").Deprecated.Should().BeFalse();
    }

    private static string Attestations(JsonObject statement) => new JsonObject
    {
        ["attestations"] = new JsonArray(
            new JsonObject { ["predicateType"] = "https://github.com/npm/attestation/tree/main/specs/publish/v0.1", ["bundle"] = new JsonObject() },
            new JsonObject
            {
                ["predicateType"] = statement["predicateType"]!.GetValue<string>(),
                ["bundle"] = new JsonObject { ["dsseEnvelope"] = new JsonObject { ["payload"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(statement.ToJsonString())) } },
            }),
    }.ToJsonString();

    [Fact]
    public void NpmProvenance_ShouldReadTheSlsaV1Workflow_AndTheRunWithoutTheAttempt()
    {
        var json = Attestations(JsonNode.Parse("""
            {
              "_type": "https://in-toto.io/Statement/v1",
              "predicateType": "https://slsa.dev/provenance/v1",
              "predicate": {
                "buildDefinition": { "externalParameters": { "workflow": { "ref": "refs/heads/main", "repository": "https://github.com/sigstore/sigstore-js", "path": ".github/workflows/release.yml" } } },
                "runDetails": { "builder": { "id": "https://github.com/actions/runner/github-hosted" }, "metadata": { "invocationId": "https://github.com/sigstore/sigstore-js/actions/runs/4194012597/attempts/1" } }
              }
            }
            """)!.AsObject());

        var provenance = RegistryParsers.ParseNpmProvenance(json);

        provenance.Should().Be(new ProvenanceRead("https://github.com/sigstore/sigstore-js", ".github/workflows/release.yml", "https://github.com/sigstore/sigstore-js/actions/runs/4194012597"));
    }

    [Fact]
    public void NpmProvenance_ShouldReadSlsaV02_AndIgnoreBrokenPayloads()
    {
        var json = Attestations(JsonNode.Parse("""
            {
              "predicateType": "https://slsa.dev/provenance/v0.2",
              "predicate": {
                "invocation": { "configSource": { "uri": "git+https://github.com/acme/old@refs/heads/main", "entryPoint": ".github/workflows/publish.yml" } },
                "metadata": { "buildInvocationId": "https://github.com/acme/old/actions/runs/1/attempts/2" }
              }
            }
            """)!.AsObject());

        RegistryParsers.ParseNpmProvenance(json).Should().Be(new ProvenanceRead("https://github.com/acme/old", ".github/workflows/publish.yml", "https://github.com/acme/old/actions/runs/1"));
        RegistryParsers.ParseNpmProvenance("""{ "attestations": [ { "predicateType": "https://slsa.dev/provenance/v1", "bundle": { "dsseEnvelope": { "payload": "not base64!" } } } ] }""").Should().BeNull();
        RegistryParsers.ParseNpmProvenance("{}").Should().BeNull();
    }

    #endregion

    #region NuGet

    [Fact]
    public void NuGet_ShouldReadLeavesUnlistedVersionsAndDeprecation()
    {
        const string index = """
            {
              "count": 2,
              "items": [
                { "@id": "https://api.nuget.org/v3/registration5-gz-semver2/acme.lib/index.json#page/1.0.0/1.1.0", "count": 2, "lower": "1.0.0", "upper": "1.1.0",
                  "items": [
                    { "@id": "x", "catalogEntry": { "id": "Acme.Lib", "version": "1.0.0", "published": "2025-01-01T00:00:00+00:00", "listed": false, "projectUrl": "https://github.com/acme/lib" } },
                    { "@id": "y", "catalogEntry": { "id": "Acme.Lib", "version": "1.1.0", "published": "1900-01-01T00:00:00+00:00", "listed": false } }
                  ] },
                { "@id": "https://api.nuget.org/v3/registration5-gz-semver2/acme.lib/page/2.0.0/3.0.0-rc.1.json", "count": 2, "lower": "2.0.0", "upper": "3.0.0-rc.1" }
              ]
            }
            """;
        const string page = """
            { "items": [
                { "catalogEntry": { "id": "Acme.Lib", "version": "2.0.0", "published": "2026-02-01T08:00:00+00:00",
                                    "deprecation": { "reasons": [ "Legacy" ], "message": "Use Acme.Next" } } },
                { "catalogEntry": { "id": "Acme.Lib", "version": "3.0.0-rc.1", "published": "2026-03-01T08:00:00+00:00", "deprecation": { "reasons": [ "CriticalBugs" ] } } }
            ] }
            """;

        var pages = RegistryParsers.ParseNuGetIndex(index);
        var read = RegistryParsers.ParseNuGetLeaves([.. pages[0].Leaves!, .. RegistryParsers.ParseNuGetPage(page)]);

        pages.Should().HaveCount(2);
        pages[1].Leaves.Should().BeNull();
        pages[1].PageUrl.Should().EndWith("page/2.0.0/3.0.0-rc.1.json");
        read.Name.Should().Be("Acme.Lib");
        read.LatestVersion.Should().Be("2.0.0", "the highest listed stable version");
        read.Repository.Should().Be("https://github.com/acme/lib");
        read.Versions.Single(v => v.Version == "1.1.0").Should().BeEquivalentTo(new { Unlisted = true, Published = (DateTime?)null });
        read.Versions.Single(v => v.Version == "2.0.0").Should().BeEquivalentTo(new { Deprecated = true, Message = "Use Acme.Next" });
        read.Versions.Single(v => v.Version == "3.0.0-rc.1").Message.Should().Be("CriticalBugs");
        read.Versions.Should().OnlyContain(v => v.HasProvenance == false);
    }

    #endregion

    #region PyPI

    [Fact]
    public void PyPI_ShouldReadReleasesYanksAndTheSourceUrl()
    {
        const string json = """
            {
              "info": { "name": "SampleProject", "version": "4.0.0", "home_page": null,
                        "project_urls": { "Homepage": "https://example.com", "Source": "https://github.com/pypa/sampleproject/" } },
              "releases": {
                "1.0": [],
                "3.0.0": [ { "filename": "sampleproject-3.0.0.tar.gz", "upload_time_iso_8601": "2022-01-01T10:00:00.000000Z", "yanked": true, "yanked_reason": "Broken" } ],
                "4.0.0": [
                  { "filename": "sampleproject-4.0.0-py3-none-any.whl", "upload_time_iso_8601": "2024-11-06T12:00:05.000000Z", "yanked": false, "yanked_reason": null },
                  { "filename": "sampleproject-4.0.0.tar.gz", "upload_time_iso_8601": "2024-11-06T12:00:00.000000Z", "yanked": false, "yanked_reason": null }
                ]
              },
              "urls": [], "vulnerabilities": []
            }
            """;

        var read = RegistryParsers.ParsePyPI(json);

        read.Name.Should().Be("SampleProject");
        read.LatestVersion.Should().Be("4.0.0");
        read.Repository.Should().Be("https://github.com/pypa/sampleproject/");
        read.Latest.Published.Should().Be(new DateTime(2024, 11, 6, 12, 0, 0, DateTimeKind.Utc), "the first upload of the release");
        read.Latest.ArtifactFile.Should().Be("sampleproject-4.0.0-py3-none-any.whl");
        read.Latest.HasProvenance.Should().BeNull("PyPI answers provenance per file");
        read.Versions.Single(v => v.Version == "3.0.0").Should().BeEquivalentTo(new { Yanked = true, Message = "Broken" });
        read.Versions.Single(v => v.Version == "1.0").Should().BeEquivalentTo(new { Yanked = false, Published = (DateTime?)null });
    }

    [Fact]
    public void PyPIProvenance_ShouldReadThePublisher()
    {
        const string json = """
            { "version": 1, "attestation_bundles": [ { "publisher": { "claims": null, "environment": "", "kind": "GitHub", "repository": "pypa/sampleproject", "workflow": "release.yml" }, "attestations": [] } ] }
            """;

        RegistryParsers.ParsePyPIProvenance(json).Should().Be(new ProvenanceRead("https://github.com/pypa/sampleproject", "release.yml", null));
        RegistryParsers.ParsePyPIProvenance("""{ "version": 1, "attestation_bundles": [] }""").Should().BeNull();
    }

    #endregion

    #region crates.io

    [Fact]
    public void Crate_ShouldReadVersionsYanksAndTrustedPublishing()
    {
        const string json = """
            {
              "crate": { "id": "demo", "name": "demo", "max_version": "2.0.0-beta.1", "max_stable_version": "1.1.0", "newest_version": "2.0.0-beta.1", "repository": "https://github.com/acme/demo", "versions": [3, 2, 1] },
              "versions": [
                { "id": 3, "num": "2.0.0-beta.1", "created_at": "2026-06-01T00:00:00.000000Z", "yanked": false, "yank_message": null,
                  "trustpub_data": { "provider": "gitlab", "project_path": "acme/demo", "job_id": "55", "sha": "abc" } },
                { "id": 2, "num": "1.1.0", "created_at": "2026-05-01T00:00:00.000000Z", "yanked": false, "yank_message": null,
                  "trustpub_data": { "provider": "github", "repository": "acme/demo", "run_id": "16612", "sha": "abc" } },
                { "id": 1, "num": "1.0.0", "created_at": "2026-04-01T00:00:00.000000Z", "yanked": true, "yank_message": "Unsound", "trustpub_data": null }
              ]
            }
            """;

        var read = RegistryParsers.ParseCrate(json);

        read.LatestVersion.Should().Be("1.1.0");
        read.Repository.Should().Be("https://github.com/acme/demo");
        read.Latest.Should().BeEquivalentTo(new { HasProvenance = (bool?)true, CiRunUrl = "https://github.com/acme/demo/actions/runs/16612", SourceRepository = "https://github.com/acme/demo" });
        read.Versions[0].CiRunUrl.Should().Be("https://gitlab.com/acme/demo/-/jobs/55");
        read.Versions[2].Should().BeEquivalentTo(new { Yanked = true, Message = "Unsound", HasProvenance = (bool?)false });
    }

    #endregion

    #region OSV.dev

    [Fact]
    public void Osv_ShouldReadBatchIdsAndAdvisoryDetails()
    {
        RegistryParsers.ParseOsvBatch("""{ "results": [ { "vulns": [ { "id": "GHSA-1", "modified": "x" }, { "id": "RUSTSEC-2", "modified": "x" } ] }, {} ] }""")
                       .Should().BeEquivalentTo(new List<List<string>> { new() { "GHSA-1", "RUSTSEC-2" }, new() });

        var github = RegistryParsers.ParseOsvVulnerability("""
            { "id": "GHSA-jfh8-c2jp-5v3q", "summary": "Remote code injection in Log4j", "severity": [ { "type": "CVSS_V3", "score": "CVSS:3.1/AV:N" } ],
              "database_specific": { "severity": "CRITICAL", "github_reviewed": true } }
            """);
        var fuzz = RegistryParsers.ParseOsvVulnerability("""
            { "id": "OSV-2020-1", "details": "Heap overflow\nin the parser", "affected": [ { "ecosystem_specific": { "severity": "high" } } ] }
            """);

        github.Should().Be(new RegistryAdvisory("GHSA-jfh8-c2jp-5v3q", "CRITICAL", "Remote code injection in Log4j", "https://osv.dev/vulnerability/GHSA-jfh8-c2jp-5v3q"));
        fuzz.Severity.Should().Be("HIGH");
        fuzz.Summary.Should().Be("Heap overflow");
        RegistryParsers.ParseOsvVulnerability("""{ "id": "GHSA-x", "withdrawn": "2026-01-01T00:00:00Z" }""").Should().BeNull();
    }

    [Theory]
    [InlineData("git+https://github.com/a/b.git", "https://github.com/a/b")]
    [InlineData("git://github.com/a/b.git", "https://github.com/a/b")]
    [InlineData("git+https://github.com/a/b@refs/heads/main", "https://github.com/a/b")]
    [InlineData("github:a/b", null)]
    [InlineData("", null)]
    public void GitUrl_ShouldBecomeABrowsableAddress(string url, string expected) => RegistryParsers.GitUrl(url).Should().Be(expected);

    #endregion

    #region Sync rules

    private static TrackedPackage Package(PackageRegistry registry = PackageRegistry.Npm, bool synced = true)
        => new() { Id = 5, OrganizationId = 9, Registry = registry, Name = "demo", SyncedDate = synced ? _now.AddDays(-1) : null };

    private static RegistryPackageRead Read(string latest, params RegistryVersionRead[] versions)
        => new() { Name = "demo", LatestVersion = latest, Repository = "https://github.com/acme/demo", Versions = [.. versions] };

    private static RegistryVersionRead Ver(string version, int day, bool? provenance = true, bool deprecated = false, bool yanked = false, string message = null)
        => new() { Version = version, Published = _now.AddDays(day), HasProvenance = provenance, Deprecated = deprecated, Yanked = yanked, Message = message, CiRunUrl = provenance == true ? $"https://ci/{version}" : null };

    private static TrackedPackageVersion Row(string version, bool? provenance = true, bool deprecated = false, bool yanked = false)
        => new() { TrackedPackageId = 5, OrganizationId = 9, Version = version, HasProvenance = provenance, IsDeprecated = deprecated, IsYanked = yanked };

    [Fact]
    public void FirstSync_ShouldReportNoVersionChange_ButAlertOnTheLatestVersion()
    {
        var package = Package(synced: false);
        var read = Read("2.0.0", Ver("1.0.0", -10, deprecated: true), Ver("2.0.0", -1, provenance: false, deprecated: true, message: "Gone"));

        var outcome = RegistrySyncRules.Merge(package, read, null, [], [], [], _now, 100);

        outcome.AddedVersions.Should().HaveCount(2);
        outcome.Changes.Should().BeEmpty();
        outcome.OpenedAlerts.Select(a => (a.Kind, a.Version)).Should().BeEquivalentTo([(PackageAlertKind.Deprecated, "2.0.0"), (PackageAlertKind.NoProvenance, "2.0.0")],
                                                                                    "an old version deprecated before tracking started is history");
        package.DeprecationMessage.Should().Be("Gone");
        package.HasProvenance.Should().BeFalse();
        package.LatestCiRunUrl.Should().BeNull();
    }

    [Fact]
    public void LaterSync_ShouldReportNewDeprecatedAndYankedVersions()
    {
        var package = Package(PackageRegistry.Crates);
        var read = Read("1.2.0", Ver("1.0.0", -10, yanked: true, message: "Unsound"), Ver("1.1.0", -5, deprecated: true), Ver("1.2.0", -1));
        var rows = new[] { Row("1.0.0"), Row("1.1.0") };

        var outcome = RegistrySyncRules.Merge(package, read, ".github/workflows/release.yml", rows, [], [], _now, 100);

        outcome.Changes.Should().BeEquivalentTo([
            new RegistryChange(RegistryChangeKind.Yanked, "1.0.0", "Unsound"),
            new RegistryChange(RegistryChangeKind.Deprecated, "1.1.0", null),
            new RegistryChange(RegistryChangeKind.NewVersion, "1.2.0", null),
        ]);
        outcome.UpdatedVersions.Select(v => v.Version).Should().BeEquivalentTo(["1.0.0", "1.1.0"]);
        outcome.OpenedAlerts.Select(a => (a.Kind, a.Version)).Should().BeEquivalentTo([(PackageAlertKind.Yanked, "1.0.0"), (PackageAlertKind.Deprecated, "1.1.0")]);
        package.LatestVersion.Should().Be("1.2.0");
        package.LatestCiRunUrl.Should().Be("https://ci/1.2.0");
        package.LatestWorkflow.Should().Be(".github/workflows/release.yml");
        package.SyncedDate.Should().Be(_now.AddDays(-1), "the syncer sets the sync date");
    }

    [Fact]
    public void Alerts_ShouldStayOpenWhileTheConditionHolds_AndResolveWhenItIsGone()
    {
        var package = Package();
        var rows = new[] { Row("1.0.0", yanked: true), Row("1.1.0", provenance: false) };
        var open = new List<PackageAlert>
        {
            new() { Id = 1, Kind = PackageAlertKind.Yanked, Version = "1.0.0" },
            new() { Id = 2, Kind = PackageAlertKind.NoProvenance, Version = "1.1.0" },
            new() { Id = 3, Kind = PackageAlertKind.Advisory, Version = "1.1.0", AdvisoryId = "GHSA-old" },
        };
        var read = Read("1.2.0", Ver("1.0.0", -10, yanked: true), Ver("1.1.0", -5, provenance: false), Ver("1.2.0", -1));

        var outcome = RegistrySyncRules.Merge(package, read, null, rows, open, [new RegistryAdvisory("GHSA-new", "HIGH", "Bad", "https://osv.dev/vulnerability/GHSA-new")], _now, 100);

        outcome.ResolvedAlerts.Select(a => a.Id).Should().BeEquivalentTo([2L, 3L]);
        outcome.ResolvedAlerts.Should().OnlyContain(a => a.ResolvedDate == _now);
        outcome.OpenedAlerts.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Kind = PackageAlertKind.Advisory, AdvisoryId = "GHSA-new", Version = "1.2.0", Severity = "HIGH", OrganizationId = 9, TrackedPackageId = 5 });
        outcome.Changes.Should().Contain(new RegistryChange(RegistryChangeKind.Advisory, "1.2.0", "GHSA-new Bad"));
    }

    [Fact]
    public void UnreadAdvisories_ShouldKeepTheOpenAdvisoryAlerts_AndNuGetShouldNeverMissProvenance()
    {
        var package = Package(PackageRegistry.NuGet);
        var open = new List<PackageAlert> { new() { Id = 7, Kind = PackageAlertKind.Advisory, Version = "1.0.0", AdvisoryId = "GHSA-a" } };
        var read = Read("1.0.0", Ver("1.0.0", -1, provenance: false));

        var outcome = RegistrySyncRules.Merge(package, read, null, [Row("1.0.0", provenance: false)], open, null, _now, 100);

        outcome.ResolvedAlerts.Should().BeEmpty();
        outcome.OpenedAlerts.Should().BeEmpty();
    }

    [Fact]
    public void NewVersions_ShouldBeCappedInTheReport_AndProvenanceReadsApplied()
    {
        var package = Package(PackageRegistry.PyPI);
        var versions = Enumerable.Range(1, 8).Select(i => Ver($"1.{i}.0", i - 10, provenance: null)).ToArray();
        var read = Read("1.8.0", versions);

        RegistrySyncRules.ApplyProvenance(read, new Dictionary<string, ProvenanceRead> { ["1.8.0"] = new("https://github.com/acme/demo", "release.yml", null), ["1.7.0"] = null });

        var outcome = RegistrySyncRules.Merge(package, read, "release.yml", [Row("0.1.0")], [], [], _now, 3);

        outcome.AddedVersions.Select(v => v.Version).Should().BeEquivalentTo(["1.8.0", "1.7.0", "1.6.0"], "only the newest versions are kept");
        outcome.Changes.Count(c => c.Kind == RegistryChangeKind.NewVersion).Should().Be(3);
        outcome.AddedVersions.Single(v => v.Version == "1.7.0").HasProvenance.Should().BeFalse();
        outcome.AddedVersions.Single(v => v.Version == "1.6.0").HasProvenance.Should().BeNull();
        package.HasProvenance.Should().BeTrue();
        package.SourceRepository.Should().Be("https://github.com/acme/demo");
    }

    #endregion
}
