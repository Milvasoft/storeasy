using FluentAssertions;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Rules;
using Storeasy.Domain.Stores;
using Storeasy.Domain.Winget;
using Storeasy.Infrastructure.Stores.Winget;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "winget manifests, planning, review states and credentials.")]
public class WingetTests
{
    private const string Id = "Milvasoft.Kabuk";

    private static readonly string VersionYaml = """
        # Created using wingetcreate 1.12.8.0
        # yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.12.0.schema.json

        PackageIdentifier: Milvasoft.Kabuk
        PackageVersion: 1.2.0
        DefaultLocale: en-US
        ManifestType: version
        ManifestVersion: 1.12.0
        """.ReplaceLineEndings("\n");

    private static readonly string InstallerYaml = """
        PackageIdentifier: Milvasoft.Kabuk
        PackageVersion: 1.2.0
        InstallerType: exe
        InstallerSwitches:
          Silent: /S
          SilentWithProgress: /S
        UpgradeBehavior: install
        Installers:
        - Architecture: x64
          Scope: user
          InstallerUrl: https://download.milvasoft.com/kabuk/1.2.0/KabukSetup-x64.exe
          InstallerSha256: 0000000000000000000000000000000000000000000000000000000000000001
        - Architecture: arm64
          Scope: user
          InstallerUrl: https://download.milvasoft.com/kabuk/1.2.0/KabukSetup-arm64.exe
          InstallerSha256: 0000000000000000000000000000000000000000000000000000000000000002
        ManifestType: installer
        ManifestVersion: 1.12.0
        ReleaseDate: 2026-10-01
        """.ReplaceLineEndings("\n");

    private static readonly string LocaleYaml = """
        PackageIdentifier: Milvasoft.Kabuk
        PackageVersion: 1.2.0
        PackageLocale: en-US
        Publisher: Milvasoft
        PublisherUrl: https://milvasoft.com
        PackageName: Kabuk
        License: Proprietary
        ShortDescription: 'Terminal: fast and tidy'
        Description: |-
          Kabuk is a terminal.
          It is fast.
        Moniker: kabuk
        Tags:
        - terminal
        - shell
        ReleaseNotes: Old notes
        ManifestType: defaultLocale
        ManifestVersion: 1.12.0
        """.ReplaceLineEndings("\n");

    private static readonly string TurkishYaml = """
        PackageIdentifier: Milvasoft.Kabuk
        PackageVersion: 1.2.0
        PackageLocale: tr-TR
        ShortDescription: "Hızlı ve düzenli terminal"
        ManifestType: locale
        ManifestVersion: 1.12.0
        """.ReplaceLineEndings("\n");

    private static Dictionary<string, string> Files() => new()
    {
        [$"{Id}.yaml"] = VersionYaml,
        [$"{Id}.installer.yaml"] = InstallerYaml,
        [$"{Id}.locale.en-US.yaml"] = LocaleYaml,
        [$"{Id}.locale.tr-TR.yaml"] = TurkishYaml,
    };

    private static StoreContentSnapshot Snapshot(Dictionary<string, string> files = null, params string[] versions)
        => WingetContent.ToSnapshot(Id, versions.Length == 0 ? ["1.2.0", "1.1.0"] : versions, files == null || files.Count > 0 ? "1.2.0" : null, files ?? Files());

    private static WingetPlanInput NewVersion(string version = "1.3.0", StoreContentSnapshot snapshot = null, IReadOnlyDictionary<string, int> open = null) => new(
        Id,
        snapshot ?? Snapshot(),
        WingetChangeKind.NewVersion,
        version,
        [
            new() { Url = "https://download.milvasoft.com/kabuk/1.3.0/KabukSetup-x64.exe", Architecture = "x64", Scope = "user" },
            new() { Url = "https://download.milvasoft.com/kabuk/1.3.0/KabukSetup-arm64.exe", Architecture = "arm64", Scope = "user", Sha256 = new string('a', 64) },
        ],
        [new() { Locale = "en-US", Fields = new() { ["ReleaseNotes"] = "Faster startup." } }],
        open ?? new Dictionary<string, int>(),
        new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Yaml_ParsesWingetManifests_AndWritesThemBack()
    {
        var map = WingetYaml.Parse(LocaleYaml);

        map.Text("ShortDescription").Should().Be("Terminal: fast and tidy");
        map.Text("Description").Should().Be("Kabuk is a terminal.\nIt is fast.");
        map.Seq("Tags").Should().Equal("terminal", "shell");

        var written = WingetYaml.Write(map);

        written.Should().Contain("ShortDescription: 'Terminal: fast and tidy'").And.Contain("Description: |-\n  Kabuk is a terminal.\n  It is fast.\n").And.Contain("Tags:\n- terminal\n- shell\n");
        WingetYaml.Write(WingetYaml.Parse(written)).Should().Be(written);
    }

    [Fact]
    public void Yaml_ReadsSequencesOfMappings_NestedMappings_AndQuotedScalars()
    {
        var map = WingetYaml.Parse(InstallerYaml);
        var installers = map.Seq("Installers").OfType<YamlMap>().ToList();

        installers.Should().HaveCount(2);
        installers[1].Text("Architecture").Should().Be("arm64");
        map.Map("InstallerSwitches").Text("Silent").Should().Be("/S");
        map.Text("ReleaseDate").Should().Be("2026-10-01");

        WingetYaml.Parse(TurkishYaml).Text("ShortDescription").Should().Be("Hızlı ve düzenli terminal");
        WingetYaml.Parse("A: 'it''s'\nB: \"x\\ty\"\nC: [a, 'b c']\nD: {}\n").Should().SatisfyRespectively(
            a => a.Value.Should().Be("it's"),
            b => b.Value.Should().Be("x\ty"),
            c => c.Value.Should().BeEquivalentTo(new List<object> { "a", "b c" }),
            d => d.Value.Should().BeOfType<YamlMap>());
    }

    [Fact]
    public void Yaml_RejectsWhatItCannotRead()
    {
        var act = () => WingetYaml.Parse("A: 1\n  B: 2\n");

        act.Should().Throw<WingetYamlException>();
        ((Action)(() => WingetYaml.Parse("A: &x 1\n"))).Should().Throw<WingetYamlException>();
    }

    [Fact]
    public void Versions_FollowWingetOrder()
    {
        WingetVersions.Descending(["1.2", "1.10.0", "1.2.34-beta", "1.2.34", "v1.9", "1.2.0.1"])
                      .Should().Equal("1.10.0", "v1.9", "1.2.34", "1.2.34-beta", "1.2.0.1", "1.2");
        WingetVersions.Comparer.Compare("1.2", "1.2.0").Should().Be(0);
    }

    [Fact]
    public void Paths_FollowTheRepositoryLayout()
    {
        WingetPaths.VersionFolder("Microsoft.PowerToys", "0.99.1").Should().Be("manifests/m/Microsoft/PowerToys/0.99.1");
        WingetPaths.SchemaUrl("defaultLocale", "1.12.0").Should().Be("https://aka.ms/winget-manifest.defaultLocale.1.12.0.schema.json");
        WingetClient.IsVersionFolder("1.2.0").Should().BeTrue();
        WingetClient.IsVersionFolder("v2").Should().BeTrue();
        WingetClient.IsVersionFolder("Beta").Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsAValidSet_AndReportsSchemaAndPolicyProblems()
    {
        WingetManifestValidator.Validate(WingetManifestSet.FromFiles(Files())).Should().BeEmpty();

        var broken = Files();

        broken[$"{Id}.installer.yaml"] = InstallerYaml.Replace("InstallerSwitches:\n  Silent: /S\n  SilentWithProgress: /S\n", string.Empty)
                                                      .Replace("Architecture: arm64", "Architecture: x64")
                                                      .Replace("https://download.milvasoft.com/kabuk/1.2.0/KabukSetup-x64.exe", "https://cdn.example.net/KabukSetup-x64.exe");
        broken[$"{Id}.locale.en-US.yaml"] = LocaleYaml.Replace("License: Proprietary\n", string.Empty);

        var issues = WingetManifestValidator.Validate(WingetManifestSet.FromFiles(broken));

        issues.Should().Contain(i => i.Key == MessageKey.WingetFieldRequired && (string)i.Args[0] == "License" && i.Blocking);
        issues.Should().Contain(i => i.Key == MessageKey.WingetInstallerNotSilent && i.Blocking);
        issues.Should().Contain(i => i.Key == MessageKey.WingetInstallerDuplicate && i.Blocking);
        issues.Should().Contain(i => i.Key == MessageKey.WingetInstallerDomain && !i.Blocking);
    }

    [Fact]
    public void Validator_RefusesSingletonsMismatchesAndFonts()
    {
        var files = Files();

        files[$"{Id}.locale.tr-TR.yaml"] = TurkishYaml.Replace("PackageVersion: 1.2.0", "PackageVersion: 1.1.0");
        files[$"{Id}.installer.yaml"] = InstallerYaml.Replace("InstallerType: exe", "InstallerType: font");

        var issues = WingetManifestValidator.Validate(WingetManifestSet.FromFiles(files));

        issues.Should().Contain(i => i.Key == MessageKey.WingetFieldMismatch && (string)i.Args[0] == "PackageVersion");
        issues.Should().Contain(i => i.Key == MessageKey.WingetInstallerTypeNotAllowed);
    }

    [Fact]
    public void Planner_BuildsANewVersionFromTheLatest()
    {
        var result = WingetPlanner.Build(NewVersion());

        result.IsBlocked.Should().BeFalse(string.Join("; ", result.Issues.Select(i => i.Key)));

        var change = (WingetPlannedChange)result.Draft.CustomChanges;
        var installer = change.Files.Single(f => f.Path.EndsWith(".installer.yaml", StringComparison.Ordinal));
        var locale = change.Files.Single(f => f.Path.EndsWith(".locale.en-US.yaml", StringComparison.Ordinal));

        change.Files.Select(f => f.Path).Should().OnlyContain(p => p.StartsWith("manifests/m/Milvasoft/Kabuk/1.3.0/", StringComparison.Ordinal)).And.HaveCount(4);
        installer.Content.Should().Contain("PackageVersion: 1.3.0").And.Contain("Silent: /S").And.Contain(WingetManifestValidator.PendingSha256)
                         .And.Contain(new string('A', 64)).And.NotContain("ReleaseDate").And.NotContain("1.2.0/KabukSetup");
        installer.Content.Should().StartWith("# Created with storeasy\n# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.12.0.schema.json\n\n");
        locale.Content.Should().Contain("ReleaseNotes: Faster startup.").And.NotContain("Old notes");
        change.Installers.Should().HaveCount(2);
        change.Installers[0].ExpectedSha256.Should().BeNull();
        change.Installers[1].ExpectedSha256.Should().Be(new string('A', 64));
        change.Title.Should().Be("New version: Milvasoft.Kabuk version 1.3.0");
        change.Branch.Should().Be("storeasy/Milvasoft.Kabuk-1.3.0-20261122100000");
        result.Draft.ConfirmText.Should().Be("Milvasoft.Kabuk 1.3.0");
        result.Draft.RequiredPermission.Should().Be(OrgPermission.SubmitForReview);
        result.Draft.Items.Should().Contain(i => i.Group == WingetPlanner.ListingGroup && i.Field == "ReleaseNotes" && i.After == "Faster startup.");
    }

    [Fact]
    public void Planner_RefusesDuplicates()
    {
        WingetPlanner.Build(NewVersion("1.1.0")).Issues.Should().Contain(i => i.Key == MessageKey.WingetVersionExists && i.Blocking);
        WingetPlanner.Build(NewVersion(open: new Dictionary<string, int> { ["1.3.0"] = 12 })).Issues.Should().Contain(i => i.Key == MessageKey.WingetVersionPullRequestOpen && i.Blocking);
        WingetPlanner.Build(NewVersion() with { Installers = [] }).Issues.Should().Contain(i => i.Key == MessageKey.WingetInstallersRequired);
    }

    [Fact]
    public void Planner_NeedsTheDefaultLocaleFieldsForANewPackage()
    {
        var empty = WingetContent.ToSnapshot(Id, [], null, new Dictionary<string, string>());
        var input = NewVersion(snapshot: empty) with
        {
            Installers = [new() { Url = "https://download.milvasoft.com/k.msi", Architecture = "x64", Type = "msi" }],
            Locales = [new() { Locale = "en-US", Fields = new() { ["Publisher"] = "Milvasoft", ["PackageName"] = "Kabuk" } }],
        };

        var blocked = WingetPlanner.Build(input);

        blocked.IsBlocked.Should().BeTrue();
        blocked.Issues.Select(i => i.Args.FirstOrDefault()).Should().Contain("License").And.Contain("ShortDescription");

        input.Locales[0].Fields["License"] = "MIT";
        input.Locales[0].Fields["ShortDescription"] = "A terminal.";

        var result = WingetPlanner.Build(input);

        result.IsBlocked.Should().BeFalse(string.Join("; ", result.Issues.Select(i => $"{i.Key} {string.Join(",", i.Args)}")));
        ((WingetPlannedChange)result.Draft.CustomChanges).Title.Should().StartWith("New package: ");
    }

    [Fact]
    public void Planner_EditsLocaleTextsOfAVersion()
    {
        var input = new WingetPlanInput(Id, Snapshot(), WingetChangeKind.Listing, null,
                                        [],
                                        [new() { Locale = "tr-TR", Fields = new() { ["ShortDescription"] = "Hızlı terminal", ["Description"] = "Kabuk bir terminaldir." } }, new() { Locale = "de-DE", Fields = new() { ["ShortDescription"] = "Schnelles Terminal" } }],
                                        new Dictionary<string, int>(),
                                        DateTime.UtcNow);

        var result = WingetPlanner.Build(input);

        result.IsBlocked.Should().BeFalse(string.Join("; ", result.Issues.Select(i => i.Key)));

        var change = (WingetPlannedChange)result.Draft.CustomChanges;

        change.Installers.Should().BeEmpty();
        change.Title.Should().Be("Update metadata: Milvasoft.Kabuk version 1.2.0");
        change.Files.Single(f => f.Path.EndsWith(".locale.de-DE.yaml", StringComparison.Ordinal)).Content.Should().Contain("ManifestType: locale");
        result.Draft.BaseKeys.Should().Contain(WingetContent.ReadPrefix + "1.2.0");
        result.Draft.Items.Where(i => i.Group == WingetPlanner.ManifestGroup).Select(i => i.Field).Should().NotContain(f => f.EndsWith(".installer.yaml", StringComparison.Ordinal));

        var same = input with { Locales = [new() { Locale = "tr-TR", Fields = new() { ["ShortDescription"] = "Hızlı ve düzenli terminal" } }] };

        WingetPlanner.Build(same).Issues.Should().Contain(i => i.Key == MessageKey.WingetNoChanges);
        WingetPlanner.Build(input with { Locales = [new() { Locale = "tr-TR", Fields = new() { ["Moniker"] = "x" } }] }).Issues.Should().Contain(i => i.Key == MessageKey.WingetFieldNotEditable);
    }

    [Fact]
    public void Content_FlattensTheListingAndHashesFiles()
    {
        var snapshot = Snapshot();

        snapshot.Get(ContentKey.Of(ContentGroups.Listing, "en-US", "PackageName")).Should().Be("Kabuk");
        snapshot.Get(ContentKey.Of(ContentGroups.Listing, "tr-TR", "ShortDescription")).Should().Be("Hızlı ve düzenli terminal");
        snapshot.Locales.Should().Equal("en-US", "tr-TR");
        WingetContent.VersionsOf(snapshot).Should().Equal("1.2.0", "1.1.0");
        WingetContent.Hash(Files()).Should().Be(WingetContent.Hash(Files().Reverse().ToDictionary()));
    }

    [Fact]
    public void PlanExtension_WritesTheComputedHashes()
    {
        var change = (WingetPlannedChange)WingetPlanner.Build(NewVersion()).Draft.CustomChanges;
        var files = WingetPlanExtension.WithHashes(change.Files, new Dictionary<int, string> { [0] = new string('B', 64), [1] = new string('A', 64) });
        var set = WingetManifestSet.FromFiles(files.ToDictionary(f => f.Path[(f.Path.LastIndexOf('/') + 1)..], f => f.Content));

        WingetContent.InstallersOf(set).Select(i => i.Sha256).Should().Equal(new string('B', 64), new string('A', 64));
        WingetManifestValidator.Validate(set).Should().NotContain(i => i.Blocking);
    }

    [Theory]
    [InlineData(true, false, false, new string[0], WingetPullRequestState.Validating)]
    [InlineData(true, false, false, new[] { "Azure-Pipeline-Passed", "Validation-Completed" }, WingetPullRequestState.ValidationCompleted)]
    [InlineData(true, false, false, new[] { "Validation-Completed", "Moderator-Approved" }, WingetPullRequestState.Approved)]
    [InlineData(true, false, false, new[] { "Needs-Author-Feedback", "Validation-Completed" }, WingetPullRequestState.NeedsAuthorFeedback)]
    [InlineData(true, false, false, new[] { "Error-Hash-Mismatch" }, WingetPullRequestState.ValidationFailed)]
    [InlineData(true, false, false, new[] { "Validation-Unattended-Failed" }, WingetPullRequestState.ValidationFailed)]
    [InlineData(true, false, false, new[] { "Validation-Domain", "Waived-Validation-Domain", "Validation-Completed" }, WingetPullRequestState.ValidationCompleted)]
    [InlineData(true, false, false, new[] { "Internal-Error-Dynamic-Scan" }, WingetPullRequestState.Validating)]
    [InlineData(true, false, true, new string[0], WingetPullRequestState.Draft)]
    [InlineData(false, true, false, new[] { "Publish-Pipeline-Succeeded" }, WingetPullRequestState.Merged)]
    [InlineData(false, false, false, new[] { "No-Recent-Activity" }, WingetPullRequestState.Closed)]
    public void ReviewStates_FollowTheLabels(bool open, bool merged, bool draft, string[] labels, WingetPullRequestState expected)
        => WingetReviewStates.Of(open, merged, draft, labels, []).Should().Be(expected);

    [Fact]
    public void ReviewStates_ReadAFailedCheckRun()
        => WingetReviewStates.Of(true, false, false, [], [new WingetCheck { Name = "Validation", Status = "completed", Conclusion = "failure" }]).Should().Be(WingetPullRequestState.ValidationFailed);

    [Fact]
    public void Credentials_AreCheckedOffline_AndFingerprintedWithoutTheToken()
    {
        var token = "ghp_" + new string('x', 36);
        var inspection = WingetChannel.InspectSecret($$"""{"token":" {{token}} ","fork":"https://github.com/Octo-Cat/winget-pkgs.git"}""");

        inspection.IsValid.Should().BeTrue();
        inspection.Fingerprint.Repository.Should().Be("Octo-Cat/winget-pkgs");
        inspection.Fingerprint.AccountLogin.Should().Be("Octo-Cat");
        inspection.Fingerprint.TokenKind.Should().Be(WingetCredentialParser.ClassicToken);
        inspection.NormalizedSecret.Should().Contain(token).And.Contain("\"fork\":\"Octo-Cat/winget-pkgs\"");
        System.Text.Json.JsonSerializer.Serialize(inspection.Fingerprint).Should().NotContain(token);

        WingetChannel.InspectSecret("""{"token":"github_pat_11AAAAAAA0123456789abcdefghijklmnopqrstuvwxyz","fork":"o/winget-pkgs"}""").Fingerprint.TokenKind.Should().Be(WingetCredentialParser.FineGrainedToken);
        WingetChannel.InspectSecret("""{"token":"abc","fork":"o/winget-pkgs"}""").ErrorKey.Should().Be(MessageKey.WingetTokenInvalid);
        WingetChannel.InspectSecret($$"""{"token":"{{token}}","fork":"not a fork"}""").ErrorKey.Should().Be(MessageKey.WingetForkInvalid);
        WingetChannel.InspectSecret("{}").ErrorKey.Should().Be(MessageKey.WingetSecretInvalid);
    }

    [Fact]
    public void Channel_KeepsAReplacementOnTheSameFork()
    {
        var channel = new WingetChannel(null);
        var current = new ConnectionFingerprint { Repository = "octo/winget-pkgs" };

        channel.SameAccount(current, new ConnectionFingerprint { Repository = "Octo/winget-pkgs" }).Should().BeTrue();
        channel.SameAccount(current, new ConnectionFingerprint { Repository = "other/winget-pkgs" }).Should().BeFalse();
        channel.SyncJob(3, 7).Kind.Should().Be(StoreJobKind.WingetSync);
    }

    [Fact]
    public void Comments_AreCleanedAndShortened()
    {
        var comment = JsonNode.Parse("""
            {
              "user": { "login": "microsoft-github-policy-service[bot]", "type": "Bot" },
              "body": "[![Validation Pipeline Badge](https://example.com/badge)](https://example.com) Validation has completed.\n\n<!-- Policy app identification -->",
              "html_url": "https://github.com/microsoft/winget-pkgs/pull/1#issuecomment-1",
              "created_at": "2026-11-18T01:00:00Z"
            }
            """);

        var read = WingetClient.Comment(comment);

        read.IsBot.Should().BeTrue();
        read.Excerpt.Should().Be("Validation has completed.");
    }

    [Fact]
    public async Task Hash_IsUpperCaseSha256_AndRefusesOversizedFiles()
    {
        var bytes = Encoding.UTF8.GetBytes("installer");
        var hash = await WingetClient.HashAsync(new MemoryStream(bytes), 100, CancellationToken.None);

        hash.Sha256.Should().Be(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        hash.Size.Should().Be(bytes.Length);

        var act = () => WingetClient.HashAsync(new MemoryStream(bytes), 3, CancellationToken.None);

        await act.Should().ThrowAsync<StoreCallException>();
    }

    [Fact]
    public void Sync_ReadsVersionsFromTitles_AndRecordsStateChanges()
    {
        WingetSyncJobHandler.VersionFromTitle("New version: Milvasoft.Kabuk version 1.3.0").Should().Be("1.3.0");
        WingetSyncJobHandler.VersionFromTitle("Milvasoft.Kabuk 2.0 (#123)").Should().Be("2.0");
        WingetSyncJobHandler.VersionFromTitle("Remove Milvasoft.Kabuk").Should().BeNull();

        var pull = new WingetPullRequest { State = WingetPullRequestState.Validating, StatusHistory = [new WingetStateChange { State = WingetPullRequestState.Validating }] };

        WingetSyncJobHandler.Merge(pull, new WingetPullRequestRead { Open = true, Labels = ["Moderator-Approved"], HeadSha = "abc" }, DateTime.UtcNow);

        pull.State.Should().Be(WingetPullRequestState.Approved);
        pull.StatusHistory.Select(h => h.State).Should().Equal(WingetPullRequestState.Validating, WingetPullRequestState.Approved);
    }

    [Fact]
    public void Channels_HaveOneRowPerStore()
    {
        foreach (var store in Enum.GetValues<StoreKind>())
        {
            var row = StoreChannels.Of(store);

            row.Should().NotBeNull($"{store} needs a StoreChannels row");
            RuleStores.Parse(row.Key).Should().Be(store);
            StoreConnectionIcons.LogoOf(store).Should().Be(row.LogoIcon);
            StoreConnectionIcons.IsKnown(row.LogoIcon).Should().BeTrue();
        }

        StoreChannels.All.Select(c => c.Key).Should().OnlyHaveUniqueItems();
        StoreChannels.HasMetrics(StoreKind.Winget).Should().BeFalse();
        StoreChannels.HasReviews(StoreKind.Winget).Should().BeFalse();
        StoreChannels.CredentialExpiryRequired(StoreKind.Winget).Should().BeFalse();
        StoreChannels.CredentialExpiryRequired(StoreKind.Microsoft).Should().BeTrue();
        StoreConnectionIcons.IsAllowed(StoreKind.Winget, StoreConnectionIcons.Winget).Should().BeTrue();
        StoreConnectionIcons.IsAllowed(StoreKind.Apple, StoreConnectionIcons.Winget).Should().BeFalse();
    }
}
