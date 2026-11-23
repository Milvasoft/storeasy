using FluentAssertions;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Package registries releases: next versions, version checks, version files, release settings, workflow detection and credentials.")]
public class RegistryReleaseTests
{
    #region Versions

    [Fact]
    public void Next_ShouldSuggestPatchMinorMajorAndPrerelease()
    {
        var next = ReleaseRules.Next("11.0.2", "rc.1");

        next.Select(n => (n.Kind, n.Version)).Should().Equal(("patch", "11.0.3"), ("minor", "11.1.0"), ("major", "12.0.0"), ("prerelease", "11.0.3-rc.1"));
        ReleaseRules.Next("2.0.0-beta.1").First().Version.Should().Be("2.0.0");
        ReleaseRules.Next(null).First().Version.Should().Be("0.0.1");
    }

    [Theory]
    [InlineData(PackageRegistry.Npm, "1.2.3", true)]
    [InlineData(PackageRegistry.Npm, "1.2.3-rc.1+build.5", true)]
    [InlineData(PackageRegistry.Npm, "1.2", false)]
    [InlineData(PackageRegistry.Crates, "01.2.3", false)]
    [InlineData(PackageRegistry.NuGet, "11.0.2", true)]
    [InlineData(PackageRegistry.NuGet, "1.2.3.4", true)]
    [InlineData(PackageRegistry.NuGet, "1.2.3.4.5", false)]
    [InlineData(PackageRegistry.PyPI, "1.2.3rc1", true)]
    [InlineData(PackageRegistry.PyPI, "1.2.3.post1", true)]
    [InlineData(PackageRegistry.PyPI, "1.2.3-x y", false)]
    [InlineData(PackageRegistry.RubyGems, "1.2.3.pre", true)]
    [InlineData(PackageRegistry.Maven, "", false)]
    public void IsValidVersion_ShouldFollowEachRegistry(PackageRegistry registry, string version, bool valid)
        => ReleaseRules.IsValidVersion(registry, version).Should().Be(valid);

    [Fact]
    public void TagOf_ShouldFillThePattern()
    {
        ReleaseRules.TagOf("sdk-rust-v{version}", "1.0.0").Should().Be("sdk-rust-v1.0.0");
        ReleaseRules.TagOf("v{version}", "11.2.1").Should().Be("v11.2.1");
    }

    #endregion

    #region Version files

    [Fact]
    public void Locate_ShouldPreferPackageVersionInMsBuild()
    {
        const string props = "<Project>\r\n  <PropertyGroup>\r\n    <Version>1.0.0</Version>\r\n    <PackageVersion>11.0.2</PackageVersion>\r\n  </PropertyGroup>\r\n</Project>\r\n";

        var location = ReleaseRules.Locate(props, VersionFileFormat.MsBuild);
        var after = ReleaseRules.Replace(props, location, "11.0.3");

        location.Version.Should().Be("11.0.2");
        location.Line.Should().Be(4);
        ReleaseRules.LineAt(after, location.Index).Should().Be("    <PackageVersion>11.0.3</PackageVersion>");
        after.Should().Contain("<Version>1.0.0</Version>").And.Contain("\r\n");
        ReleaseRules.Locate("<Project><Version>2.1.0</Version></Project>", VersionFileFormat.MsBuild).Version.Should().Be("2.1.0");
    }

    [Theory]
    [InlineData(VersionFileFormat.PackageJson, "{\n  \"name\": \"@milvasoft/milvaion-sdk\",\n  \"version\": \"1.4.0\",\n  \"dependencies\": { \"x\": \"2.0.0\" }\n}", "1.4.0", 3)]
    [InlineData(VersionFileFormat.Toml, "[package]\nname = \"milvaion-worker\"\nversion = \"0.3.1\"\n\n[dependencies]\nserde = { version = \"1\" }\n", "0.3.1", 3)]
    [InlineData(VersionFileFormat.Toml, "[project]\nname = \"milvaion\"\nversion = \"2.0.0\"\n", "2.0.0", 3)]
    [InlineData(VersionFileFormat.Ruby, "Gem::Specification.new do |spec|\n  spec.name = \"milvaion\"\n  spec.version = \"0.2.0\"\nend\n", "0.2.0", 3)]
    [InlineData(VersionFileFormat.Ruby, "module Milvaion\n  VERSION = '1.1.0'\nend\n", "1.1.0", 2)]
    [InlineData(VersionFileFormat.Pom, "<project>\n  <parent>\n    <version>3.0.0</version>\n  </parent>\n  <artifactId>sdk</artifactId>\n  <version>1.2.0</version>\n</project>", "1.2.0", 6)]
    [InlineData(VersionFileFormat.PlainText, "4.5.6\n", "4.5.6", 1)]
    public void Locate_ShouldFindTheVersionOfEachFormat(VersionFileFormat format, string content, string version, int line)
    {
        var location = ReleaseRules.Locate(content, format);

        location.Version.Should().Be(version);
        location.Line.Should().Be(line);
        ReleaseRules.Replace(content, location, "9.9.9").Should().Contain("9.9.9");
    }

    [Fact]
    public void Locate_ShouldReturnNullWithoutVersion()
    {
        ReleaseRules.Locate("{ \"name\": \"x\" }", VersionFileFormat.PackageJson).Should().BeNull();
        ReleaseRules.Locate("a\nb\n", VersionFileFormat.PlainText).Should().BeNull();
        ReleaseRules.Locate(string.Empty, VersionFileFormat.Toml).Should().BeNull();
    }

    #endregion

    #region Settings

    [Fact]
    public void Problems_ShouldCheckEachTrigger()
    {
        ReleaseRules.Problems(null).Should().Equal(MessageKey.RegistryReleaseNotSet);

        ReleaseRules.Problems(new ReleaseSettings { Repository = "Milvasoft/Milvasoft", Trigger = ReleaseTrigger.VersionBump, VersionFiles = [new VersionFileSetting { Path = "Directory.Build.props", Format = VersionFileFormat.MsBuild }] })
                    .Should().BeEmpty();

        ReleaseRules.Problems(new ReleaseSettings { Repository = "bad", Branch = "a..b", Trigger = ReleaseTrigger.VersionBumpAndTag, TagPattern = "v1" })
                    .Should().Contain([MessageKey.RegistrySetupRepositoryInvalid, MessageKey.RegistryReleaseBranchInvalid, MessageKey.RegistryReleaseVersionFilesInvalid, MessageKey.RegistryReleaseTagPatternInvalid]);

        ReleaseRules.Problems(new ReleaseSettings { Repository = "Milvasoft/milvaion", Trigger = ReleaseTrigger.Dispatch, Workflow = "release.yml", DispatchVersionInput = "version" }).Should().BeEmpty();
        ReleaseRules.Problems(new ReleaseSettings { Repository = "Milvasoft/milvaion", Trigger = ReleaseTrigger.Dispatch, Workflow = "release", DispatchVersionInput = "ver sion" })
                    .Should().Contain([MessageKey.RegistryReleaseDispatchInvalid, MessageKey.RegistrySetupWorkflowInvalid]);
        ReleaseRules.Problems(new ReleaseSettings { Repository = "Milvasoft/milvaion", Trigger = ReleaseTrigger.VersionBump, VersionFiles = [new VersionFileSetting { Path = "../etc/passwd", Format = VersionFileFormat.PlainText }] })
                    .Should().Contain(MessageKey.RegistryReleaseVersionFilesInvalid);
    }

    [Fact]
    public void DispatchInputs_ShouldPassVersionOrTagAndPrerelease()
    {
        var settings = new ReleaseSettings { DispatchVersionInput = "version", DispatchPrereleaseInput = "prerelease" };

        ReleaseRules.DispatchInputs(settings, "10.0.12-rc.1", null).Should().BeEquivalentTo(new Dictionary<string, string> { ["version"] = "10.0.12-rc.1", ["prerelease"] = "true" });
        ReleaseRules.DispatchInputs(new ReleaseSettings { DispatchVersionInput = "tag", DispatchTakesTag = true }, "1.0.0", null).Should().BeEquivalentTo(new Dictionary<string, string> { ["tag"] = "v1.0.0" });
    }

    #endregion

    #region Detection

    private const string _milvasoftPackageWorkflow = """
        name: Milvasoft.BulkExtensions.PostgreSql Nuget Publish

        on:
          push:
            branches: [ master ]
            paths:
              - 'src/BulkExtensions/Milvasoft.BulkExtensions.PostgreSql/**'
              - '**/Directory.Build.props'

        jobs:
          publish:
            uses: ./.github/workflows/nuget-publish.yml
            with:
              project: src/BulkExtensions/Milvasoft.BulkExtensions.PostgreSql/Milvasoft.BulkExtensions.PostgreSql.csproj
        """;

    private const string _rustWorkflow = """
        name: Rust SDK - Publish
        on:
          push:
            tags:
              - 'sdk-rust-v*'
        jobs:
          publish:
            runs-on: ubuntu-latest
            steps:
              - run: cargo publish --locked
        """;

    private const string _releaseWorkflow = """
        on:
          push:
            tags:
              - 'v*.*.*'
          workflow_dispatch:
            inputs:
              version:
                description: 'Version to release (e.g., 10.0.12)'
                required: true
              prerelease:
                description: 'Is this a pre-release?'
                type: boolean
        jobs:
          release:
            steps:
              - run: dotnet nuget push "*.nupkg" --api-key x
        """;

    [Fact]
    public void Detection_ShouldSuggestAVersionBumpOfDirectoryBuildProps()
    {
        var facts = ReleaseDetection.Analyze("bulkextensions-postgresql.yml", _milvasoftPackageWorkflow);
        var suggestions = ReleaseDetection.Suggest("Milvasoft/Milvasoft", "main", [facts], PackageRegistry.NuGet, "Milvasoft.BulkExtensions.PostgreSql");

        facts.Registries.Should().Contain(PackageRegistry.NuGet);
        facts.Branches.Should().Equal("master");
        suggestions.Should().ContainSingle();
        suggestions[0].Settings.Trigger.Should().Be(ReleaseTrigger.VersionBump);
        suggestions[0].Settings.Branch.Should().Be("master");
        suggestions[0].Settings.VersionFiles.Should().ContainSingle(f => f.Path == "Directory.Build.props" && f.Format == VersionFileFormat.MsBuild);
    }

    [Fact]
    public void Detection_ShouldSuggestTagsAndDispatch()
    {
        var workflows = new[] { ReleaseDetection.Analyze("sdk-rust-publish.yml", _rustWorkflow), ReleaseDetection.Analyze("release.yml", _releaseWorkflow) };

        var rust = ReleaseDetection.Suggest("Milvasoft/milvaion", "master", workflows, PackageRegistry.Crates, "milvaion-worker");
        var nuget = ReleaseDetection.Suggest("Milvasoft/milvaion", "master", workflows, PackageRegistry.NuGet, "Milvaion.Worker.Sdk");

        rust.Should().ContainSingle(s => s.Settings.Trigger == ReleaseTrigger.Tag && s.Settings.TagPattern == "sdk-rust-v{version}" && s.Settings.Workflow == "sdk-rust-publish.yml");
        nuget.Should().Contain(s => s.Settings.Trigger == ReleaseTrigger.Tag && s.Settings.TagPattern == "v{version}");
        nuget.Should().Contain(s => s.Settings.Trigger == ReleaseTrigger.Dispatch && s.Settings.DispatchVersionInput == "version" && s.Settings.DispatchPrereleaseInput == "prerelease");
        nuget.Should().NotContain(s => s.Settings.Workflow == "sdk-rust-publish.yml");
    }

    [Theory]
    [InlineData("Directory.Build.props", VersionFileFormat.MsBuild)]
    [InlineData("sdks/node/package.json", VersionFileFormat.PackageJson)]
    [InlineData("sdks/rust/milvaion-worker/Cargo.toml", VersionFileFormat.Toml)]
    [InlineData("sdks/ruby/milvaion.gemspec", VersionFileFormat.Ruby)]
    [InlineData("sdks/java/pom.xml", VersionFileFormat.Pom)]
    [InlineData("VERSION", VersionFileFormat.PlainText)]
    public void FormatOf_ShouldFollowTheFileName(string path, VersionFileFormat format) => ReleaseDetection.FormatOf(path).Should().Be(format);

    #endregion

    #region Credentials

    [Fact]
    public void Secrets_ShouldShowOnlyTheEndAndMapRegistries()
    {
        RegistrySecrets.Fingerprint("oy2abcdefghijklmnopqrstuvwxyz1234").Should().Be("****1234");
        RegistrySecrets.Fingerprint("short").Should().Be("****");
        RegistrySecrets.Context(7, RegistryConnectionKind.NuGet).Should().Be("RegistryConnection:7:NuGet");
        RegistrySecrets.KindOf(PackageRegistry.PyPI).Should().BeNull();
        RegistrySecrets.KindOf(PackageRegistry.Crates).Should().Be(RegistryConnectionKind.Crates);
        RegistrySecrets.RegistryOf(RegistryConnectionKind.GitHub).Should().BeNull();
    }

    [Theory]
    [InlineData(PackageRegistry.NuGet, RegistryPlanKind.Unlist, true)]
    [InlineData(PackageRegistry.NuGet, RegistryPlanKind.Deprecate, false)]
    [InlineData(PackageRegistry.Npm, RegistryPlanKind.DistTag, true)]
    [InlineData(PackageRegistry.Crates, RegistryPlanKind.Unyank, true)]
    [InlineData(PackageRegistry.RubyGems, RegistryPlanKind.Unyank, false)]
    [InlineData(PackageRegistry.PyPI, RegistryPlanKind.Yank, false)]
    [InlineData(PackageRegistry.Npm, RegistryPlanKind.Release, false)]
    public void Supports_ShouldListTheOfficialWriteApis(PackageRegistry registry, RegistryPlanKind kind, bool supported)
        => RegistrySecrets.Supports(registry, kind).Should().Be(supported);

    #endregion
}
