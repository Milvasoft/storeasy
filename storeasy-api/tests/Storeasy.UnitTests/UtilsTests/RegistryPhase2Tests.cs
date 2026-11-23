using FluentAssertions;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Registries;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Package registries phase 2: Maven Central, RubyGems, Packagist and pub.dev answers, the trusted publishing wizard and check, token reminders.")]
public class RegistryPhase2Tests
{
    private static readonly DateTime _today = new(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

    #region Names

    [Theory]
    [InlineData(PackageRegistry.Maven, "com.google.code.gson:gson", true)]
    [InlineData(PackageRegistry.Maven, "gson", false)]
    [InlineData(PackageRegistry.RubyGems, "rake", true)]
    [InlineData(PackageRegistry.RubyGems, "-rake", false)]
    [InlineData(PackageRegistry.Packagist, "monolog/monolog", true)]
    [InlineData(PackageRegistry.Packagist, "Monolog/Monolog", false)]
    [InlineData(PackageRegistry.Packagist, "monolog", false)]
    [InlineData(PackageRegistry.Pub, "http", true)]
    [InlineData(PackageRegistry.Pub, "flutter_bloc", true)]
    [InlineData(PackageRegistry.Pub, "Http", false)]
    [InlineData(PackageRegistry.Pub, "1http", false)]
    public void Name_ShouldFollowTheNewRegistriesRules(PackageRegistry registry, string name, bool valid)
        => RegistryNames.IsValid(registry, name).Should().Be(valid);

    [Fact]
    public void NewRegistries_ShouldHaveTheirEcosystemsAndProvenanceSupport()
    {
        RegistryNames.OsvEcosystem(PackageRegistry.Maven).Should().Be("Maven");
        RegistryNames.OsvEcosystem(PackageRegistry.Pub).Should().Be("Pub");
        RegistryNames.OsvEcosystem(PackageRegistry.Packagist).Should().Be("Packagist");
        RegistryNames.OsvEcosystem(PackageRegistry.RubyGems).Should().Be("RubyGems");
        RegistryNames.SupportsProvenance(PackageRegistry.RubyGems).Should().BeTrue();
        RegistryNames.SupportsProvenance(PackageRegistry.Maven).Should().BeFalse();
        RegistryNames.SupportsProvenance(PackageRegistry.Packagist).Should().BeFalse();
        RegistryNames.SupportsProvenance(PackageRegistry.Pub).Should().BeFalse();
        RegistryNames.PageUrl(PackageRegistry.Maven, "com.google.code.gson:gson").Should().Be("https://central.sonatype.com/artifact/com.google.code.gson/gson");
    }

    #endregion

    #region Parsers

    [Fact]
    public void Maven_ShouldReadMetadataVersionsAndSearchDates()
    {
        const string xml = """
            <metadata>
              <groupId>com.google.code.gson</groupId><artifactId>gson</artifactId>
              <versioning><latest>2.15.0</latest><release>2.15.0</release>
                <versions><version>2.13.2-rc1</version><version>2.14.0</version><version>2.15.0</version></versions>
                <lastUpdated>20261024012745</lastUpdated></versioning>
            </metadata>
            """;
        const string search = """{ "response": { "numFound": 1, "docs": [ { "id": "com.google.code.gson:gson:2.14.0", "g": "com.google.code.gson", "a": "gson", "v": "2.14.0", "timestamp": 1776970907000 } ] } }""";

        var read = RegistryParsers.ParseMaven("com.google.code.gson:gson", xml, search);

        read.LatestVersion.Should().Be("2.15.0");
        read.Versions.Select(v => v.Version).Should().Equal("2.13.2-rc1", "2.14.0", "2.15.0");
        read.Versions.Single(v => v.Version == "2.14.0").Published.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1776970907000).UtcDateTime);
        read.Latest.Published.Should().BeNull("the search index lags behind the repository");
        RegistryParsers.ParseMaven("x:y", "not xml", null).Should().BeNull();
    }

    [Fact]
    public void RubyGems_ShouldReadGemAndVersionsOncePerNumber()
    {
        const string gem = """{ "name": "rake", "version": "13.4.2", "metadata": { "source_code_uri": "https://github.com/ruby/rake" } }""";
        const string versions = """
            [ { "number": "13.4.2", "created_at": "2026-10-01T00:00:00.000Z", "platform": "ruby" },
              { "number": "13.4.2", "created_at": "2026-10-01T00:05:00.000Z", "platform": "java" },
              { "number": "13.4.1", "created_at": "2026-09-01T00:00:00.000Z", "platform": "ruby", "yanked": true } ]
            """;

        var read = RegistryParsers.ParseRubyGems(gem, versions);

        read.Name.Should().Be("rake");
        read.Repository.Should().Be("https://github.com/ruby/rake");
        read.Versions.Should().HaveCount(2);
        read.Latest.Published.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        read.Versions.Single(v => v.Version == "13.4.1").Yanked.Should().BeTrue();
    }

    [Fact]
    public void RubyGemsAttestations_ShouldReadTheSlsaPayload_AndCountUnreadableBundles()
    {
        var statement = new JsonObject
        {
            ["predicateType"] = "https://slsa.dev/provenance/v1",
            ["predicate"] = JsonNode.Parse("""{ "buildDefinition": { "externalParameters": { "workflow": { "repository": "https://github.com/ruby/rake", "path": ".github/workflows/push.yml" } } }, "runDetails": { "metadata": { "invocationId": "https://github.com/ruby/rake/actions/runs/9/attempts/1" } } }"""),
        };
        var json = new JsonArray(new JsonObject { ["dsseEnvelope"] = new JsonObject { ["payload"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(statement.ToJsonString())) } }).ToJsonString();

        RegistryParsers.ParseRubyGemsAttestations(json).Should().Be(new ProvenanceRead("https://github.com/ruby/rake", ".github/workflows/push.yml", "https://github.com/ruby/rake/actions/runs/9"));
        RegistryParsers.ParseRubyGemsAttestations("""[ { "messageSignature": {} } ]""").Should().Be(new ProvenanceRead(null, null, null));
        RegistryParsers.ParseRubyGemsAttestations("[]").Should().BeNull();
    }

    [Fact]
    public void Packagist_ShouldExpandMinifiedMetadata_AndReadAbandoned()
    {
        const string json = """
            { "minified": "composer/2.0", "packages": { "acme/lib": [
              { "name": "acme/lib", "version": "3.1.0", "version_normalized": "3.1.0.0", "time": "2026-11-01T10:00:00+00:00", "source": { "url": "https://github.com/acme/lib.git" }, "abandoned": "acme/next", "extra": { "x": 1 } },
              { "version": "3.1.0-RC1", "version_normalized": "3.1.0.0-RC1", "time": "2026-10-20T10:00:00+00:00", "extra": "__unset" },
              { "version": "3.0.0", "version_normalized": "3.0.0.0", "time": "2026-09-01T10:00:00+00:00" }
            ] } }
            """;

        var read = RegistryParsers.ParsePackagist("acme/lib", json);

        read.Versions.Select(v => v.Version).Should().Equal("3.1.0", "3.1.0-RC1", "3.0.0");
        read.LatestVersion.Should().Be("3.1.0");
        read.Repository.Should().Be("https://github.com/acme/lib");
        read.Latest.Should().BeEquivalentTo(new { Deprecated = true, Message = "Abandoned; use acme/next" });
        read.Versions[2].Published.Should().Be(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Pub_ShouldReadVersionsRetractionsAndDiscontinued()
    {
        const string json = """
            { "name": "http", "latest": { "version": "1.6.0", "pubspec": { "repository": "https://github.com/dart-lang/http" }, "published": "2025-11-10T18:27:56.434747Z" },
              "versions": [ { "version": "1.5.0", "published": "2025-01-01T00:00:00Z", "retracted": true }, { "version": "1.6.0", "published": "2025-11-10T18:27:56.434747Z" } ] }
            """;

        var read = RegistryParsers.ParsePub(json, """{ "isDiscontinued": true, "replacedBy": "http2", "isUnlisted": false }""");

        read.LatestVersion.Should().Be("1.6.0");
        read.Repository.Should().Be("https://github.com/dart-lang/http");
        read.Versions.Single(v => v.Version == "1.5.0").Yanked.Should().BeTrue();
        read.Latest.Should().BeEquivalentTo(new { Deprecated = true, Message = "Discontinued; use http2" });
        RegistryParsers.ParsePub(json, null).Latest.Deprecated.Should().BeFalse();
    }

    #endregion

    #region Trusted publishing

    private static TrackedPackage Package(PackageRegistry registry, CiProvider? provider = CiProvider.GitHubActions, bool provenance = true, string repository = "https://github.com/acme/lib", string workflow = ".github/workflows/release.yml")
        => new()
        {
            Id = 7,
            Registry = registry,
            Name = "lib",
            LatestVersion = "1.0.0",
            HasProvenance = provenance,
            SourceRepository = repository,
            LatestWorkflow = workflow,
            SetupProvider = provider,
            SetupRepository = provider == null ? null : "acme/lib",
            SetupWorkflow = provider == CiProvider.GitLabCi ? ".gitlab-ci.yml" : "release.yml",
        };

    [Fact]
    public void Providers_ShouldFollowEachRegistrysTrustedPublishing()
    {
        TrustedPublishing.Providers(PackageRegistry.Npm).Should().Equal(CiProvider.GitHubActions, CiProvider.GitLabCi);
        TrustedPublishing.Providers(PackageRegistry.PyPI).Should().Equal(CiProvider.GitHubActions, CiProvider.GitLabCi);
        TrustedPublishing.Providers(PackageRegistry.Crates).Should().Equal(CiProvider.GitHubActions);
        TrustedPublishing.Providers(PackageRegistry.NuGet).Should().Equal(CiProvider.GitHubActions);
        TrustedPublishing.Providers(PackageRegistry.Maven).Should().BeEmpty();
        TrustedPublishing.Providers(PackageRegistry.Packagist).Should().BeEmpty();
        TrustedPublishing.SettingsUrl(PackageRegistry.Pub, "http").Should().Be("https://pub.dev/packages/http/admin");
        TrustedPublishing.SettingsUrl(PackageRegistry.PyPI, "Zope_Interface").Should().Be("https://pypi.org/manage/project/zope-interface/settings/publishing/");
    }

    [Theory]
    [InlineData(CiProvider.GitHubActions, "acme/lib", "release.yml", true)]
    [InlineData(CiProvider.GitHubActions, "acme/team/lib", "release.yml", false)]
    [InlineData(CiProvider.GitHubActions, "acme/lib", ".github/workflows/release.yml", false)]
    [InlineData(CiProvider.GitHubActions, "acme/lib", "release.txt", false)]
    [InlineData(CiProvider.GitLabCi, "acme/team/lib", ".gitlab-ci.yml", true)]
    [InlineData(CiProvider.GitLabCi, "acme/lib", "ci/../publish.yml", false)]
    public void Setup_ShouldCheckRepositoryAndWorkflow(CiProvider provider, string repository, string workflow, bool valid)
        => (TrustedPublishing.IsRepository(provider, repository) && TrustedPublishing.IsWorkflow(provider, workflow)).Should().Be(valid);

    [Fact]
    public void RegistryFields_ShouldMatchTheRegistrysForm()
    {
        TrustedPublishing.RegistryFields(PackageRegistry.Npm, CiProvider.GitHubActions, "acme/lib", "release.yml", "npm")
                         .Should().Equal(new SetupField("owner", "acme"), new SetupField("repository", "lib"), new SetupField("workflow", "release.yml"), new SetupField("environment", "npm"));
        TrustedPublishing.RegistryFields(PackageRegistry.PyPI, CiProvider.GitLabCi, "acme/team/lib", ".gitlab-ci.yml", null)
                         .Should().Equal(new SetupField("namespace", "acme/team"), new SetupField("project", "lib"), new SetupField("pipelineFile", ".gitlab-ci.yml"));
        TrustedPublishing.RegistryFields(PackageRegistry.Pub, CiProvider.GitHubActions, "acme/lib", "publish.yml", null)
                         .Should().Equal(new SetupField("repositoryPath", "acme/lib"), new SetupField("tagPattern", "v{{version}}"));
    }

    [Theory]
    [InlineData(PackageRegistry.Npm, CiProvider.GitHubActions, "npm publish --provenance")]
    [InlineData(PackageRegistry.Npm, CiProvider.GitLabCi, "aud: \"npm:registry.npmjs.org\"")]
    [InlineData(PackageRegistry.PyPI, CiProvider.GitHubActions, "pypa/gh-action-pypi-publish@release/v1")]
    [InlineData(PackageRegistry.PyPI, CiProvider.GitLabCi, "aud: pypi")]
    [InlineData(PackageRegistry.Crates, CiProvider.GitHubActions, "CARGO_REGISTRY_TOKEN: ${{ steps.auth.outputs.token }}")]
    [InlineData(PackageRegistry.NuGet, CiProvider.GitHubActions, "user: acme-bot")]
    [InlineData(PackageRegistry.RubyGems, CiProvider.GitHubActions, "rubygems/release-gem@v1")]
    [InlineData(PackageRegistry.Pub, CiProvider.GitHubActions, "dart-lang/setup-dart/.github/workflows/publish.yml@v1")]
    public void WorkflowText_ShouldPublishWithOidc(PackageRegistry registry, CiProvider provider, string expected)
    {
        var text = TrustedPublishing.WorkflowText(registry, provider, "release", "acme-bot");

        text.Should().Contain(expected);
        text.Should().Contain(provider == CiProvider.GitLabCi ? "id_tokens:" : "id-token: write");
        text.Should().Contain("release");
        text.Should().NotContain("—").And.NotContain("–");
    }

    [Fact]
    public void WorkflowText_ShouldBeNullWithoutTrustedPublishing()
    {
        TrustedPublishing.WorkflowText(PackageRegistry.Maven, CiProvider.GitHubActions, null, null).Should().BeNull();
        TrustedPublishing.WorkflowText(PackageRegistry.Crates, CiProvider.GitLabCi, null, null).Should().BeNull();
    }

    [Fact]
    public void Check_ShouldCompareTheLatestReleaseWithTheSetup()
    {
        TrustedPublishing.Check(Package(PackageRegistry.Npm, provider: null)).Status.Should().Be(PublisherCheckStatus.NotConfigured);
        TrustedPublishing.Check(Package(PackageRegistry.Npm)).Status.Should().Be(PublisherCheckStatus.Match);
        TrustedPublishing.Check(Package(PackageRegistry.PyPI, workflow: "release.yml")).Status.Should().Be(PublisherCheckStatus.Match);
        TrustedPublishing.Check(Package(PackageRegistry.Crates, workflow: null)).Status.Should().Be(PublisherCheckStatus.Match, "crates.io names the repository only");
        TrustedPublishing.Check(Package(PackageRegistry.Npm, workflow: ".github/workflows/other.yml")).Status.Should().Be(PublisherCheckStatus.Mismatch);
        TrustedPublishing.Check(Package(PackageRegistry.Npm, repository: "https://github.com/someone/fork")).Status.Should().Be(PublisherCheckStatus.Mismatch);
        TrustedPublishing.Check(Package(PackageRegistry.Npm, provenance: false)).Status.Should().Be(PublisherCheckStatus.NoProvenance);
        TrustedPublishing.Check(Package(PackageRegistry.NuGet)).Status.Should().Be(PublisherCheckStatus.NotVerifiable);
        TrustedPublishing.Check(Package(PackageRegistry.PyPI, CiProvider.GitLabCi, repository: "https://gitlab.com/acme/lib", workflow: ".gitlab-ci.yml")).Status.Should().Be(PublisherCheckStatus.Match);
    }

    [Fact]
    public void Sync_ShouldAlertWhenAReleaseComesFromAnotherWorkflow()
    {
        var package = Package(PackageRegistry.Npm);

        package.SyncedDate = _today.AddDays(-1);

        var read = new RegistryPackageRead
        {
            Name = "lib",
            LatestVersion = "1.1.0",
            Versions = [new RegistryVersionRead { Version = "1.1.0", Published = _today, HasProvenance = true, SourceRepository = "https://github.com/acme/lib", CiRunUrl = "https://ci/1" }],
        };

        var outcome = RegistrySyncRules.Merge(package, read, ".github/workflows/manual.yml", [], [], [], _today, 100);

        outcome.OpenedAlerts.Should().ContainSingle(a => a.Kind == PackageAlertKind.PublisherMismatch && a.Version == "1.1.0");
        outcome.Changes.Should().Contain(c => c.Kind == RegistryChangeKind.PublisherMismatch);
    }

    #endregion

    #region Token reminders

    [Theory]
    [InlineData(20, 0)]
    [InlineData(14, 1)]
    [InlineData(4, 1)]
    [InlineData(3, 2)]
    [InlineData(1, 2)]
    [InlineData(0, 3)]
    [InlineData(-5, 3)]
    public void Stage_ShouldWarn14And3DaysBeforeAndOnExpiry(int days, byte stage)
        => TokenReminderRules.Stage(_today.Date.AddDays(days), _today).Should().Be(stage);

    [Fact]
    public void Decide_ShouldWarnOncePerStage_AndResolveWhenTheExpiryMoves()
    {
        TokenReminderRules.Decide(0, false, _today.AddDays(10), _today).Should().Be(new ReminderDecision(true, false, true, 1));
        TokenReminderRules.Decide(1, true, _today.AddDays(10), _today).Should().Be(new ReminderDecision(false, false, false, 1));
        TokenReminderRules.Decide(1, true, _today.AddDays(2), _today).Should().Be(new ReminderDecision(false, false, true, 2));
        TokenReminderRules.Decide(2, true, _today.AddDays(-1), _today).Should().Be(new ReminderDecision(false, false, true, 3));
        TokenReminderRules.Decide(0, true, _today.AddDays(90), _today).Should().Be(new ReminderDecision(false, true, false, 0));
    }

    [Theory]
    [InlineData("npm publish token", false)]
    [InlineData("NPM_TOKEN in acme/lib", false)]
    [InlineData("NUGET_API_KEY secret of the release pipeline", false)]
    [InlineData("npm_A1b2C3d4E5f6G7h8I9j0K1l2", true)]
    [InlineData("pypi-AgEIcHlwaS5vcmcCJGQ0", true)]
    [InlineData("oy2abcdefghijklmnopqrstuvwxyz0123456789abcdef", true)]
    [InlineData("cioAbCdEfGhIjKlMnOpQrStUv", true)]
    [InlineData("token ghp_0123456789abcdefghij0123456789", true)]
    [InlineData("dGhpcyBpcyBhIHNlY3JldCB2YWx1ZSAxMjM0NTY3ODkw", true)]
    public void LooksLikeToken_ShouldRefuseTokenValues(string text, bool token) => TokenReminderRules.LooksLikeToken(text).Should().Be(token);

    #endregion
}
