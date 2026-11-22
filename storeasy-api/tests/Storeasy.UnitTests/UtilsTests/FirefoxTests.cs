using FluentAssertions;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Firefox;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Stores;
using Storeasy.Infrastructure.Stores.Firefox;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Firefox Add-ons credentials, versions, manifests, planning, AMO mapping and review states.")]
public class FirefoxTests
{
    private const string Guid = "kabuk@milvasoft.com";

    private static string Secret(string issuer = "user:12345:67", string secret = null)
        => JsonSerializer.Serialize(new { issuer, secret = secret ?? new string('a', 64) });

    private static AmoVersionRead Version(long id, string version, FirefoxVersionChannel channel = FirefoxVersionChannel.Listed, FirefoxFileStatus status = FirefoxFileStatus.Public)
        => new() { Id = id, Version = version, Channel = channel, FileStatus = status, ReleaseNotes = new() { ["en-US"] = $"Notes {version}" } };

    private static FirefoxAddonState State(params AmoVersionRead[] versions)
    {
        var addon = new AmoAddonRead
        {
            Id = 42,
            Guid = Guid,
            Slug = "kabuk",
            Type = "extension",
            Status = FirefoxAddonStatus.Public,
            DefaultLocale = "en-US",
            Name = new() { ["en-US"] = "Kabuk Tabs", ["tr"] = "Kabuk Sekmeler" },
            Summary = new() { ["en-US"] = "Tabs that stay tidy." },
            Description = new() { ["en-US"] = "Long text." },
            Categories = ["tabs"],
            Previews = [new AmoPreviewRead(7, "https://cdn/7.png", "https://cdn/7t.png", 0, null, 1280, 800)],
            CurrentVersion = versions.FirstOrDefault(v => v.Channel == FirefoxVersionChannel.Listed && v.FileStatus == FirefoxFileStatus.Public),
        };

        return new FirefoxAddonState(addon, versions);
    }

    private static ArtifactPackageInfo Package(string version, string guid = Guid, int manifestVersion = 3, bool dataCollection = true, bool minified = false)
    {
        var gecko = guid == null ? "" : $"\"browser_specific_settings\": {{ \"gecko\": {{ \"id\": \"{guid}\"{(dataCollection ? ", \"data_collection_permissions\": { \"required\": [\"none\"] }" : "")} }} }},";
        var manifest = FirefoxManifest.Parse($$"""{ "manifest_version": {{manifestVersion}}, "name": "Kabuk Tabs", "version": "{{version}}", {{gecko}} "icons": { "48": "i.png" } }""");

        if (minified)
            manifest.MinifiedScripts.Add("dist/bundle.js");

        return manifest.ToPackageInfo();
    }

    private static FirefoxPlanPackage PlanPackage(ArtifactPackageInfo package)
        => new(9, new FirefoxPlannedFile { FileName = "kabuk.xpi", ObjectKey = "k", Size = 10, Sha256 = "abc" }, package);

    [Fact]
    public void Credentials_ShouldParseTheApiKey_AndSignAnHs256Token()
    {
        // Arrange
        var secret = new string('b', 64);

        // Act
        var credentials = FirefoxCredentialParser.Parse(JsonSerializer.Serialize(new { jwtIssuer = " user:99:1 ", jwtSecret = secret }), out var error);
        var token = FirefoxCredentialParser.CreateToken(credentials, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000), 600);
        var parts = token.Split('.');
        var payload = JsonNode.Parse(Encoding.UTF8.GetString(FromBase64Url(parts[1])))!;
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"));

        // Assert
        error.Should().BeNull();
        credentials.Issuer.Should().Be("user:99:1");
        credentials.UserId.Should().Be("99");
        payload["iss"]!.GetValue<string>().Should().Be("user:99:1");
        (payload["exp"]!.GetValue<long>() - payload["iat"]!.GetValue<long>()).Should().Be(300, "AMO allows at most five minutes");
        payload["jti"].Should().NotBeNull();
        FromBase64Url(parts[2]).Should().Equal(signature);

        FirefoxCredentialParser.Parse(Secret("12345"), out var issuerError).Should().BeNull();
        issuerError.Should().Be(MessageKey.FirefoxIssuerInvalid);
        FirefoxCredentialParser.Parse(Secret(secret: "short"), out var secretError).Should().BeNull();
        secretError.Should().Be(MessageKey.FirefoxSecretInvalid);
        FirefoxCredentialParser.Parse("not json", out _).Should().BeNull();
    }

    [Fact]
    public void Channel_ShouldFingerprintWithoutTheSecret_AndKeepReplacementsOnTheSameUser()
    {
        // Act
        var inspection = FirefoxChannel.InspectSecret(Secret());
        var channel = new FirefoxChannel(null, new Storeasy.Application.Utils.Models.FirefoxOptions());
        var sameUser = FirefoxChannel.InspectSecret(Secret("user:12345:68")).Fingerprint;
        var otherUser = FirefoxChannel.InspectSecret(Secret("user:777:1")).Fingerprint;

        // Assert
        inspection.IsValid.Should().BeTrue();
        inspection.Fingerprint.KeyId.Should().Be("user:12345:67");
        inspection.Fingerprint.AccountLogin.Should().Be("user 12345");
        JsonSerializer.Serialize(inspection.Fingerprint).Should().NotContain(new string('a', 64));
        channel.SameAccount(inspection.Fingerprint, sameUser).Should().BeTrue();
        channel.SameAccount(inspection.Fingerprint, otherUser).Should().BeFalse();
        channel.CredentialLabel(inspection.Fingerprint).Should().Be("AMO API key user:12345:67");
        StoreChannels.Of(StoreKind.Firefox).HasReviews.Should().BeTrue();
        StoreChannels.Of(StoreKind.Firefox).HasMetrics.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.0", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("123456789.0", true)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("01.0", false)]
    [InlineData("1.0a1", false)]
    [InlineData("1234567890", false)]
    [InlineData("", false)]
    public void Versions_ShouldAcceptTheFormatAmoTakes(string version, bool valid) => FirefoxVersions.IsValid(version).Should().Be(valid);

    [Fact]
    public void Versions_ShouldOrderNumbersAndPreReleases()
    {
        FirefoxVersions.Compare("1.10", "1.9").Should().BePositive();
        FirefoxVersions.Compare("1.0", "1.0.0").Should().Be(0);
        FirefoxVersions.Compare("1.0a1", "1.0").Should().BeNegative();
        FirefoxVersions.Compare("2.0b2", "2.0b10").Should().BeNegative();
        FirefoxVersions.IsGuid("kabuk@milvasoft.com").Should().BeTrue();
        FirefoxVersions.IsGuid("{8e6c8f1a-1234-4bcd-9abc-0123456789ab}").Should().BeTrue();
        FirefoxVersions.IsGuid("not an id").Should().BeFalse();
        FirefoxVersions.IsAddonReference("kabuk-tabs").Should().BeTrue();
        FirefoxVersions.IsAddonReference("12345").Should().BeTrue();
        FirefoxVersions.IsAddonReference("bad slug!").Should().BeFalse();
    }

    [Fact]
    public async Task Manifest_ShouldReadTheXpi_AndFindMinifiedCode()
    {
        // Arrange
        using var memory = new MemoryStream();

        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "manifest.json", """
                // comments are allowed
                { "manifest_version": 2, "name": "Kabuk", "version": "2.1",
                  "applications": { "gecko": { "id": "kabuk@milvasoft.com", "strict_min_version": "115.0" } },
                  "permissions": ["tabs", "https://example.com/*"], "background": { "service_worker": "sw.js" }, }
                """);
            Write(archive, "dist/app.js", new string('x', FirefoxManifest.MinifiedLineLength + 10));
            Write(archive, "src/readable.js", "// fine\nconsole.log(1);\n");
        }

        memory.Position = 0;
        using var read = new ZipArchive(memory, ZipArchiveMode.Read);

        // Act
        var manifest = await FirefoxManifest.ReadAsync(read, CancellationToken.None);
        var info = manifest.ToPackageInfo();
        var issues = FirefoxManifest.IssuesOf(info);

        // Assert
        manifest.GeckoId.Should().Be(Guid);
        manifest.StrictMinVersion.Should().Be("115.0");
        manifest.HostPermissions.Should().Contain("https://example.com/*");
        manifest.MinifiedScripts.Should().Equal("dist/app.js");
        info.Kind.Should().Be(PackageKind.Xpi);
        info.Identifier.Should().Be(Guid);
        issues.Select(i => i.Key).Should().Contain([MessageKey.FirefoxManifestV2, MessageKey.FirefoxDataCollectionMissing, MessageKey.FirefoxServiceWorker, MessageKey.FirefoxSourceRequired, MessageKey.FirefoxIconMissing]);
        issues.Should().NotContain(i => i.Blocking);
    }

    [Fact]
    public void Manifest_ShouldBlockManifestV3WithoutAnId_AndBadVersions()
    {
        // Act
        var noId = FirefoxManifest.IssuesOf(Package("1.0", guid: null));
        var badVersion = FirefoxManifest.IssuesOf(Package("1.0.0.0.1"));
        var nested = new FirefoxManifest { NestedFolder = "kabuk" }.Check();

        // Assert
        noId.Should().Contain(i => i.Key == MessageKey.FirefoxGeckoIdMissing && i.Blocking);
        badVersion.Should().Contain(i => i.Key == MessageKey.FirefoxVersionInvalid && i.Blocking);
        nested.Should().ContainSingle(i => i.Key == MessageKey.FirefoxManifestNested && i.Blocking);
        FirefoxManifest.EnglishOf(MessageKey.FirefoxGeckoIdMismatch).Should().Contain("{0}");
    }

    [Fact]
    public void Planner_ShouldPlanListingValuesPerLocale_AndCheckTheirLimits()
    {
        // Arrange
        var state = State(Version(1, "1.0.0"));

        // Act
        var result = FirefoxPlanner.Build(new FirefoxPlanInput
        {
            State = state,
            Kind = FirefoxChangeKind.Listing,
            Listing =
            [
                new FirefoxListingValue { Field = "Summary", Locale = "en-US", Value = "Tidy tabs, fast." },
                new FirefoxListingValue { Field = "name", Locale = "de", Value = "Kabuk Tabs DE" },
                new FirefoxListingValue { Field = "Name", Locale = "tr", Value = "" },
                new FirefoxListingValue { Field = "Name", Locale = "en-US", Value = "Kabuk Tabs" },
            ],
            Categories = ["tabs", "privacy-security"],
        });
        var blocked = FirefoxPlanner.Build(new FirefoxPlanInput
        {
            State = state,
            Kind = FirefoxChangeKind.Listing,
            Listing =
            [
                new FirefoxListingValue { Field = "Name", Locale = "en-US", Value = "" },
                new FirefoxListingValue { Field = "Summary", Locale = "de", Value = new string('s', 251) },
                new FirefoxListingValue { Field = "Homepage", Locale = "en-US", Value = "ftp://milvasoft.com" },
                new FirefoxListingValue { Field = "SupportEmail", Locale = "en-US", Value = "not-mail" },
            ],
        });

        // Assert
        result.IsBlocked.Should().BeFalse();
        var change = (FirefoxPlannedChange)result.Draft.CustomChanges;
        change.Texts.Select(t => (t.Field, t.Locale, t.Value)).Should().Equal(("Summary", "en-US", "Tidy tabs, fast."), ("Name", "de", "Kabuk Tabs DE"), ("Name", "tr", null));
        change.Categories.Should().Equal("privacy-security", "tabs");
        result.Draft.Items.Should().Contain(i => i.Field == "Name" && i.Locale == "tr" && i.Action == ChangeAction.Delete);
        result.Draft.BaseKeys.Should().Contain([FirefoxContent.CategoriesKey, "Listing|en-US|Summary"]);
        result.Draft.ConfirmText.Should().Be(Guid);
        result.Draft.RequiredPermission.Should().BeNull();

        blocked.IsBlocked.Should().BeTrue();
        blocked.Issues.Select(i => i.Key).Should().Contain([MessageKey.FirefoxDefaultLocaleRequired, MessageKey.FirefoxTextTooLong, MessageKey.FirefoxUrlInvalid, MessageKey.FirefoxEmailInvalid]);
    }

    [Fact]
    public void Planner_ShouldPlanANewVersion_HigherThanEveryChannel_WithTheAddonsId()
    {
        // Arrange
        var state = State(Version(1, "1.0.0"), Version(2, "1.1.0", FirefoxVersionChannel.Unlisted));

        // Act
        var good = FirefoxPlanner.Build(new FirefoxPlanInput
        {
            State = state,
            Kind = FirefoxChangeKind.Version,
            Package = PlanPackage(Package("1.2.0", minified: true)),
            Channel = FirefoxVersionChannel.Listed,
            ReleaseNotes = [new FirefoxLocalizedText { Locale = "en-US", Value = "Faster." }],
            ApprovalNotes = "npm ci && npm run build",
            Source = new FirefoxPlanFile(new FirefoxPlannedFile { AssetId = 5, FileName = "src.zip", ObjectKey = "s", Size = 3 }),
        });
        var lower = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Version, Package = PlanPackage(Package("1.0.5")), Channel = FirefoxVersionChannel.Listed });
        var otherId = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Version, Package = PlanPackage(Package("1.3.0", guid: "other@milvasoft.com")), Channel = FirefoxVersionChannel.Unlisted });
        var noSource = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Version, Package = PlanPackage(Package("1.3.0", minified: true, dataCollection: false)), Channel = FirefoxVersionChannel.Unlisted });

        // Assert
        good.IsBlocked.Should().BeFalse(string.Join(", ", good.Issues.Select(i => i.Key)));
        var change = (FirefoxPlannedChange)good.Draft.CustomChanges;
        change.PackageVersion.Should().Be("1.2.0");
        change.Channel.Should().Be(FirefoxVersionChannel.Listed);
        change.Source.FileName.Should().Be("src.zip");
        change.Texts.Should().ContainSingle(t => t.Field == FirefoxFields.ReleaseNotes && t.Value == "Faster.");
        good.Draft.ConfirmText.Should().Be($"{Guid} 1.2.0");
        good.Draft.RequiredPermission.Should().Be(OrgPermission.SubmitForReview);
        good.Draft.Calls.Select(c => c.Path).Should().Contain(["/addons/upload/", "/addons/addon/42/versions/"]);

        lower.IsBlocked.Should().BeTrue();
        lower.Issues.Should().Contain(i => i.Key == MessageKey.FirefoxVersionNotHigher);
        otherId.Issues.Should().Contain(i => i.Key == MessageKey.FirefoxGeckoIdMismatch && i.Blocking);
        noSource.IsBlocked.Should().BeFalse();
        noSource.Issues.Select(i => i.Key).Should().Contain([MessageKey.FirefoxSourceMissing, MessageKey.FirefoxDataCollectionMissing]);
    }

    [Fact]
    public void Planner_ShouldAskALicenseForTheFirstListedVersion_AndCheckPreviewsAndIcons()
    {
        // Arrange
        var unlistedOnly = State(Version(1, "1.0.0", FirefoxVersionChannel.Unlisted));
        var state = State(Version(1, "1.0.0"));
        var png = new FirefoxPlanFile(new FirefoxPlannedFile { AssetId = 1, FileName = "shot.png" }, 1280, 800, "png");
        var small = new FirefoxPlanFile(new FirefoxPlannedFile { AssetId = 2, FileName = "small.png" }, 640, 400, "png");
        var gif = new FirefoxPlanFile(new FirefoxPlannedFile { AssetId = 3, FileName = "a.gif" }, 10, 10, "gif");

        // Act
        var noLicense = FirefoxPlanner.Build(new FirefoxPlanInput { State = unlistedOnly, Kind = FirefoxChangeKind.Version, Package = PlanPackage(Package("1.1.0")), Channel = FirefoxVersionChannel.Listed });
        var previews = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Previews, AddPreviews = [png, small], DeletePreviews = [7] });
        var missing = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Previews, DeletePreviews = [99] });
        var icon = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Icon, Icon = new FirefoxPlanFile(new FirefoxPlannedFile { FileName = "i.png" }, 128, 64, "png") });
        var badFormat = FirefoxPlanner.Build(new FirefoxPlanInput { State = state, Kind = FirefoxChangeKind.Previews, AddPreviews = [gif] });

        // Assert
        noLicense.Issues.Should().Contain(i => i.Key == MessageKey.FirefoxLicenseRequired && i.Blocking);
        previews.IsBlocked.Should().BeFalse();
        ((FirefoxPlannedChange)previews.Draft.CustomChanges).DeletePreviews.Should().Equal(7);
        previews.Issues.Should().ContainSingle(i => i.Key == MessageKey.FirefoxPreviewSize);
        previews.Draft.BaseKeys.Should().Contain(FirefoxContent.PreviewsKey);
        missing.Issues.Should().Contain(i => i.Key == MessageKey.FirefoxPreviewNotFound);
        icon.Issues.Should().Contain(i => i.Key == MessageKey.FirefoxIconNotSquare && i.Blocking);
        badFormat.Issues.Should().Contain(i => i.Key == MessageKey.FirefoxImageFormat);
    }

    [Fact]
    public void Planner_ShouldEditReleaseNotesOfAVersion()
    {
        // Act
        var result = FirefoxPlanner.Build(new FirefoxPlanInput
        {
            State = State(Version(1, "1.0.0"), Version(2, "1.1.0")),
            Kind = FirefoxChangeKind.ReleaseNotes,
            Version = "1.0.0",
            ReleaseNotes = [new FirefoxLocalizedText { Locale = "en-US", Value = "Better notes" }, new FirefoxLocalizedText { Locale = "de", Value = "Neu" }],
        });

        // Assert
        result.IsBlocked.Should().BeFalse();
        var change = (FirefoxPlannedChange)result.Draft.CustomChanges;
        change.VersionId.Should().Be(1);
        result.Draft.Items.Should().OnlyContain(i => i.Group == FirefoxPlanner.ReleaseNotesGroup);
        result.Draft.BaseKeys.Should().Contain($"{FirefoxContent.NotesPrefix}1");
        new FirefoxPlanExtension(null, null, null, null, null, null).AuditActionOf(ChangePlanKind.FirefoxChange, result.Draft.Items[0]).Should().Be(AuditAction.FirefoxReleaseNotesUpdated);
    }

    [Fact]
    public void Content_ShouldRoundTripTheStateThroughTheSnapshot()
    {
        // Arrange
        var state = State(Version(1, "1.0.0"), Version(2, "1.1.0", status: FirefoxFileStatus.Unreviewed));

        // Act
        var snapshot = FirefoxContent.ToSnapshot(state);
        var back = FirefoxContent.FromSnapshot(snapshot);

        // Assert
        snapshot.Get("Listing|tr|Name").Should().Be("Kabuk Sekmeler");
        snapshot.Get(FirefoxContent.LatestVersionKey).Should().Be("1.1.0");
        snapshot.Get(FirefoxContent.LatestStatusKey).Should().Be("Listed Unreviewed");
        snapshot.Get(FirefoxContent.PreviewsKey).Should().Be("7");
        snapshot.Locales.Should().Contain(["en-US", "tr"]);
        back.Addon.Name["tr"].Should().Be("Kabuk Sekmeler");
        back.Versions.Should().HaveCount(2);
        back.Latest.Version.Should().Be("1.1.0");
        back.Addon.Previews.Single().Width.Should().Be(1280);
    }

    [Fact]
    public void States_ShouldNameReviewResults_AfterTheFirstRead()
    {
        // Arrange
        var row = new FirefoxAddon();
        var now = DateTime.UtcNow;

        // Act
        var first = FirefoxAddonStates.Merge(row, State(Version(1, "1.0.0"), Version(2, "1.1.0", status: FirefoxFileStatus.Unreviewed)), now);
        var pending = row.IsPending;
        var approved = FirefoxAddonStates.Merge(row, State(Version(1, "1.0.0"), Version(2, "1.1.0")), now.AddMinutes(15));
        var rejected = FirefoxAddonStates.Merge(row, State(Version(1, "1.0.0"), Version(2, "1.1.0"), Version(3, "1.2.0", status: FirefoxFileStatus.Disabled)), now.AddMinutes(30));

        // Assert
        first.Should().BeEmpty();
        pending.Should().BeTrue();
        approved.Should().ContainSingle(t => t.Action == AuditAction.FirefoxApproved && t.Version == "1.1.0" && t.Previous == "Unreviewed");
        rejected.Should().ContainSingle(t => t.Action == AuditAction.FirefoxRejected && t.Version == "1.2.0");
        row.IsPending.Should().BeFalse();
        row.LatestVersion.Should().Be("1.2.0");
        row.StatusHistory.Should().HaveCount(4);
    }

    [Fact]
    public void Client_ShouldMapAddonsVersionsUploadsAndRatings()
    {
        // Arrange
        var addon = JsonNode.Parse("""
            { "id": 42, "guid": "kabuk@milvasoft.com", "slug": "kabuk", "type": "extension", "status": "nominated", "default_locale": "en-US",
              "name": { "en-US": "Kabuk", "de": null, "_default": "en-US" }, "summary": { "en-US": "Tidy" },
              "homepage": { "url": { "en-US": "https://milvasoft.com" }, "outgoing": { "en-US": "https://outgoing.prod/x" } },
              "categories": { "firefox": ["tabs", "appearance"] },
              "previews": [ { "id": 3, "image_url": "https://cdn/3.png", "image_size": [1280, 800], "position": 1 } ],
              "ratings": { "count": 4, "average": 4.25 }, "average_daily_users": 900, "weekly_downloads": 70,
              "current_version": { "id": 5, "version": "1.0", "channel": "listed", "file": { "status": "public", "data_collection_permissions": ["none"] }, "license": { "slug": "MIT" } } }
            """);
        var flat = JsonNode.Parse("""{ "id": 1, "categories": ["privacy-security"], "status": "public" }""");
        var upload = JsonNode.Parse("""{ "uuid": "u1", "processed": true, "valid": false, "version": "2.0", "validation": { "errors": 1, "warnings": 2, "messages": [ { "type": "warning", "message": "<b>w</b>" }, { "type": "error", "message": "bad id" } ] } }""");
        var rating = JsonNode.Parse("""{ "id": 77, "score": 2, "body": "Crashes", "created": "2026-11-20T10:00:00Z", "user": { "name": "Ayse" }, "version": { "version": "1.0" }, "reply": { "id": 78, "body": "Fixed in 1.1", "created": "2026-11-21T10:00:00Z" } }""");

        // Act
        var parsed = AmoClient.ParseAddon(addon);
        var parsedFlat = AmoClient.ParseAddon(flat);
        var parsedUpload = AmoClient.ParseUpload(upload);
        var parsedRating = AmoClient.ParseRating(rating);

        // Assert
        parsed.Status.Should().Be(FirefoxAddonStatus.Nominated);
        parsed.Name.Should().ContainKey("en-US").And.NotContainKey("de").And.NotContainKey("_default");
        parsed.Homepage["en-US"].Should().Be("https://milvasoft.com");
        parsed.Categories.Should().Equal("tabs", "appearance");
        parsed.Previews.Single().Width.Should().Be(1280);
        parsed.Rating.Should().Be(4.25);
        parsed.RatingCount.Should().Be(4);
        parsed.CurrentVersion.License.Should().Be("MIT");
        parsed.CurrentVersion.DataCollectionPermissions.Should().Equal("none");
        parsedFlat.Categories.Should().Equal("privacy-security");
        parsedUpload.Valid.Should().BeFalse();
        parsedUpload.Messages.Should().Equal("error: bad id", "warning: w");
        parsedRating.ExternalId.Should().Be("77");
        parsedRating.Rating.Should().Be(2);
        parsedRating.AppVersion.Should().Be("1.0");
        parsedRating.ReplyExternalId.Should().Be("78");
        parsedRating.ReplyBody.Should().Be("Fixed in 1.1");
        AmoClient.DescribeError("""{ "version": ["Version 1.0 already exists."] }""").Should().Be("version: Version 1.0 already exists.");
        FirefoxChannel.BuildsOf([Version(1, "1.0"), Version(2, "1.1", status: FirefoxFileStatus.Unreviewed)]).Select(b => b.ProcessingState).Should().Equal(BuildProcessingState.Valid, BuildProcessingState.Processing);
    }

    [Fact]
    public void Intake_ShouldSendXpiFilesToFirefox()
    {
        IntakeRules.Classify("kabuk-1.2.xpi").Should().Be((ArtifactPlatform.Extension, PackageKind.Xpi));
        IntakeRules.StoreOf(ArtifactPlatform.Extension, "kabuk-1.2.xpi").Should().Be(StoreKind.Firefox);
        IntakeRules.StoreOf(ArtifactPlatform.Android, "app.aab").Should().Be(StoreKind.Google);
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        using var stream = archive.CreateEntry(name).Open();

        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }
}
