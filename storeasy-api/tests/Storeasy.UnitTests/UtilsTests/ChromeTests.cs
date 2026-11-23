using FluentAssertions;
using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.Infrastructure.Stores.Chrome;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Chrome Web Store: versions, manifest checks, credentials, status mapping, planning and metrics.")]
public class ChromeTests
{
    private const string ItemId = "abcdefghijklmnopabcdefghijklmnop";

    private static ChromeItemStatusRead Status(string published = "1.0.0", int? percentage = 100, ChromeItemState submittedState = ChromeItemState.None, string submitted = null)
        => new(ItemId,
               published == null ? null : new ChromeRevisionRead(ChromeItemState.Published, published, percentage),
               submittedState == ChromeItemState.None ? null : new ChromeRevisionRead(submittedState, submitted, null),
               ChromeUploadState.None,
               false,
               false);

    private static ChromePlanInput Input(ChromeChangeKind kind, ChromeItemStatusRead status = null, ChromePlanPackage package = null, int? percentage = null, string newVersion = null, string reason = null, long? users = null)
        => new(ItemId, status ?? Status(), kind, package, false, percentage, false, null, newVersion, reason, users);

    private static ChromePlanPackage Package(string manifest)
        => new(7, "tabs.zip", "artifacts/tabs.zip", 1000, new string('a', 64), ChromeManifest.Parse(manifest).ToPackageInfo());

    [Theory]
    [InlineData("1", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("65535.0", true)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("01.2", false)]
    [InlineData("1.65536", false)]
    [InlineData("1..2", false)]
    [InlineData("1.2-beta", false)]
    [InlineData("", false)]
    public void Version_ShouldFollowTheManifestRules(string version, bool valid)
        => ChromeVersions.IsValid(version).Should().Be(valid);

    [Fact]
    public void Compare_ShouldTreatMissingPartsAsZero()
    {
        ChromeVersions.Compare("1.2", "1.2.0").Should().Be(0);
        ChromeVersions.Compare("1.10", "1.9").Should().BePositive();
        ChromeVersions.Compare("2", "1.99.99").Should().BePositive();
        ChromeVersions.IsItemId(ItemId).Should().BeTrue();
        ChromeVersions.IsItemId("abcdefghijklmnopabcdefghijklmnoq").Should().BeFalse();
    }

    [Fact]
    public void Manifest_ShouldBeReadWithCommentsAndChecked()
    {
        var manifest = ChromeManifest.Parse("""
            {
              // comments are allowed
              "manifest_version": 2,
              "name": "__MSG_appName__",
              "version": "1.0",
              "background": { "scripts": ["bg.js"] },
              "permissions": ["tabs", "storage"],
              "host_permissions": ["<all_urls>"],
              "content_scripts": [{ "matches": ["https://example.com/*"], "js": ["c.js"] }],
              "update_url": "https://example.com/updates.xml",
            }
            """);

        var issues = manifest.Check([("1.0", "published")]);

        manifest.Permissions.Should().Equal("tabs", "storage");
        manifest.HostPermissions.Should().Equal("<all_urls>", "https://example.com/*");
        issues.Where(i => i.Blocking).Select(i => i.Key).Should().BeEquivalentTo(MessageKey.ChromeManifestVersion, MessageKey.ChromeDefaultLocaleMissing, MessageKey.ChromeVersionNotHigher);
        issues.Where(i => !i.Blocking).Select(i => i.Key).Should().BeEquivalentTo(MessageKey.ChromeBackgroundPage, MessageKey.ChromeIcon128Missing, MessageKey.ChromeUpdateUrl, MessageKey.ChromeBroadHostAccess);
    }

    [Fact]
    public void Manifest_ShouldEnforceTheStoreTextLimits_AndKeepIssuesOnThePackage()
    {
        var info = ChromeManifest.Parse("{\"manifest_version\":3,\"name\":\"" + new string('n', 76) + "\",\"version\":\"2.0\",\"icons\":{\"128\":\"i.png\"}}").ToPackageInfo();
        var issues = ChromeManifest.IssuesOf(info);

        info.Kind.Should().Be(PackageKind.ExtensionZip);
        info.VersionName.Should().Be("2.0");
        info.ManifestVersion.Should().Be(3);
        issues.Should().ContainSingle();
        issues[0].Blocking.Should().BeTrue();
        issues[0].Key.Should().Be(MessageKey.ChromeTextTooLong);
        issues[0].Args.Should().Equal("name", "76", "75");
        issues[0].Text.Should().Be("name has 76 characters; the store allows 75.");
    }

    [Fact]
    public async Task Manifest_InAFolder_ShouldSayToZipTheContents()
    {
        using var memory = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("tabs/manifest.json").Open());
            writer.Write("{}");
        }

        memory.Position = 0;

        using var read = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Read);
        var manifest = await ChromeManifest.ReadAsync(read, CancellationToken.None);

        manifest.Check().Should().ContainSingle().Which.Should().Match<ChromeIssue>(i => i.Key == MessageKey.ChromeManifestNested && i.Blocking);
    }

    [Fact]
    public void Credentials_ShouldTakeAnOAuthClientOrAServiceAccount_AndNeverPutTheSecretInTheFingerprint()
    {
        using var rsa = RSA.Create(2048);
        var key = new JsonObject
        {
            ["type"] = "service_account",
            ["project_id"] = "kabuk-cws",
            ["private_key_id"] = "kid1",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "publisher@kabuk-cws.iam.gserviceaccount.com",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
        };

        var account = ChromeChannel.InspectSecret(new JsonObject { ["publisherId"] = "pub-1", ["serviceAccountKey"] = key }.ToJsonString());
        var oauth = ChromeChannel.InspectSecret(JsonSerializer.Serialize(new { publisherId = "pub-1", clientId = "123456789012-abc.apps.googleusercontent.com", clientSecret = "s3cret", refreshToken = "1//refresh" }));
        var missing = ChromeChannel.InspectSecret(JsonSerializer.Serialize(new { publisherId = "pub-1", clientId = "x" }));
        var badPublisher = ChromeChannel.InspectSecret(JsonSerializer.Serialize(new { publisherId = "pub 1/2", clientId = "x", clientSecret = "y", refreshToken = "z" }));
        var badItem = ChromeChannel.InspectSecret(JsonSerializer.Serialize(new { publisherId = "pub-1", clientId = "x", clientSecret = "y", refreshToken = "z", itemId = "nope" }));
        var badKey = ChromeChannel.InspectSecret(JsonSerializer.Serialize(new { publisherId = "pub-1", serviceAccountKey = "{\"type\":\"user\"}" }));

        account.IsValid.Should().BeTrue();
        account.Fingerprint.ServiceAccountEmail.Should().Be("publisher@kabuk-cws.iam.gserviceaccount.com");
        account.Fingerprint.ProjectId.Should().Be("kabuk-cws");
        account.Fingerprint.PublisherId.Should().Be("pub-1");
        oauth.IsValid.Should().BeTrue();
        oauth.Fingerprint.ClientIdSuffix.Should().Be("12-abc");
        oauth.Fingerprint.TokenKind.Should().Be("OAuth client");
        JsonSerializer.Serialize(oauth.Fingerprint).Should().NotContain("s3cret").And.NotContain("refresh");
        missing.ErrorKey.Should().Be(MessageKey.ChromeOAuthInvalid);
        badPublisher.ErrorKey.Should().Be(MessageKey.ChromePublisherIdInvalid);
        badItem.ErrorKey.Should().Be(MessageKey.ChromeItemIdInvalid);
        badKey.ErrorKey.Should().Be(MessageKey.ChromeServiceAccountInvalid);
        new ChromeChannel(null, new()).SameAccount(account.Fingerprint, oauth.Fingerprint).Should().BeTrue();
        new ChromeChannel(null, new()).SameAccount(account.Fingerprint, new() { PublisherId = "pub-2" }).Should().BeFalse();
    }

    [Fact]
    public void Status_ShouldMapTheApiResponse()
    {
        var status = ChromeWebStoreClient.ParseStatus(JsonNode.Parse("""
            {
              "itemId": "abcdefghijklmnopabcdefghijklmnop",
              "publishedItemRevisionStatus": { "state": "PUBLISHED", "distributionChannels": [{ "crxVersion": "1.2", "deployPercentage": 40 }] },
              "submittedItemRevisionStatus": { "state": "PENDING_REVIEW", "distributionChannels": [{ "crxVersion": "1.3" }] },
              "lastAsyncUploadState": "SUCCEEDED",
              "warned": true
            }
            """), ItemId);

        status.Published.Should().Be(new ChromeRevisionRead(ChromeItemState.Published, "1.2", 40));
        status.Submitted.Should().Be(new ChromeRevisionRead(ChromeItemState.PendingReview, "1.3", null));
        status.LastUploadState.Should().Be(ChromeUploadState.Succeeded);
        status.Warned.Should().BeTrue();
        status.TakenDown.Should().BeFalse();

        var snapshot = ChromeContent.ToSnapshot(ItemId, status);

        ChromeContent.FromSnapshot(snapshot).Should().BeEquivalentTo(status);
        snapshot.HasLiveVersion.Should().BeTrue();
    }

    [Fact]
    public void Merge_ShouldRecordHistory_AndNameTheChangesAfterTheFirstRead()
    {
        var item = new ChromeItem { ItemId = ItemId };
        var now = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

        ChromeItemStates.Merge(item, Status("1.0", submittedState: ChromeItemState.PendingReview, submitted: "1.1"), now).Should().BeEmpty();
        item.IsPending.Should().BeTrue();

        var published = ChromeItemStates.Merge(item, Status("1.1"), now.AddHours(1));

        published.Should().ContainSingle().Which.Action.Should().Be(AuditAction.ChromePublished, "a submission that went away because it was published is told once");
        item.IsPending.Should().BeFalse();

        var rejected = ChromeItemStates.Merge(item, Status("1.1", submittedState: ChromeItemState.Rejected, submitted: "1.2"), now.AddHours(2));
        var warned = ChromeItemStates.Merge(item, Status("1.1", submittedState: ChromeItemState.Rejected, submitted: "1.2") with { Warned = true }, now.AddHours(3));

        rejected.Should().ContainSingle().Which.Should().Be(new ChromeStateTransition("submitted", "None", "Rejected", "1.2", AuditAction.ChromeRejected));
        warned.Should().ContainSingle().Which.Action.Should().Be(AuditAction.ChromePolicyFlagChanged);
        item.StatusHistory.Should().HaveCount(5);
    }

    [Fact]
    public void Planner_ShouldBuildASubmission_WithEveryCallShown()
    {
        var result = ChromePlanner.Build(Input(ChromeChangeKind.Publish, package: Package("""{"manifest_version":3,"name":"Tabs","version":"1.1","icons":{"128":"i.png"}}"""), percentage: 100));
        var change = (ChromePlannedChange)result.Draft.CustomChanges;

        result.IsBlocked.Should().BeFalse();
        result.Draft.Kind.Should().Be(ChangePlanKind.ChromeChange);
        result.Draft.ConfirmText.Should().Be($"{ItemId} 1.1");
        result.Draft.RequiredPermission.Should().Be(OrgPermission.SubmitForReview);
        result.Draft.Calls.Select(c => c.Path).Should().Equal($"/upload/v2/publishers/{{publisherId}}/items/{ItemId}:upload", $"/v2/publishers/{{publisherId}}/items/{ItemId}:publish");
        result.Draft.BaseKeys.Should().Contain(ChromeContent.PublishedVersionKey);
        change.SessionId.Should().Be(7);
        change.DeployPercentage.Should().Be(100);
        change.PackageVersion.Should().Be("1.1");
    }

    [Fact]
    public void Planner_ShouldRefuseWhatTheStoreWouldRefuse()
    {
        ChromePlanner.Build(Input(ChromeChangeKind.Publish, package: Package("""{"manifest_version":3,"name":"Tabs","version":"1.0","icons":{"128":"i.png"}}"""))).Issues.Should().Contain(i => i.Key == MessageKey.ChromeVersionNotHigher && i.Blocking);
        ChromePlanner.Build(Input(ChromeChangeKind.Publish, Status(submittedState: ChromeItemState.PendingReview, submitted: "1.1"))).Issues.Should().Contain(i => i.Key == MessageKey.ChromeSubmissionPending);
        ChromePlanner.Build(Input(ChromeChangeKind.DeployPercentage, Status(percentage: 50), percentage: 50)).Issues.Should().Contain(i => i.Key == MessageKey.ChromePercentageNotHigher);
        ChromePlanner.Build(Input(ChromeChangeKind.DeployPercentage, Status(percentage: 50), percentage: 80, users: 500)).Issues.Should().Contain(i => i.Key == MessageKey.ChromePercentageUsers && !i.Blocking);
        ChromePlanner.Build(Input(ChromeChangeKind.CancelSubmission)).Issues.Should().Contain(i => i.Key == MessageKey.ChromeNothingToCancel);
        ChromePlanner.Build(Input(ChromeChangeKind.PublishStaged)).Issues.Should().Contain(i => i.Key == MessageKey.ChromeNotStaged);
        ChromePlanner.Build(Input(ChromeChangeKind.Rollback, newVersion: "0.9", reason: "x")).Issues.Should().Contain(i => i.Key == MessageKey.ChromeVersionNotHigher);
        ChromePlanner.Build(Input(ChromeChangeKind.Rollback, newVersion: "1.0.1")).Issues.Should().Contain(i => i.Key == MessageKey.ChromeRollbackReason);
        ChromePlanner.Build(Input(ChromeChangeKind.Publish)).Issues.Should().ContainSingle(i => i.Key == MessageKey.ChromeDraftSubmitted && !i.Blocking);
    }

    [Fact]
    public void Metrics_ShouldBecomeCountryAndTotalRows()
    {
        var day = new DateOnly(2026, 11, 20);
        var rows = ChromeChannel.ToMetricRows([
            new ChromeMetricRead(day, "TR", new Dictionary<string, long> { ["installs"] = 3, ["impressions"] = 100, ["page-views"] = 10 }),
            new ChromeMetricRead(day, "DE", new Dictionary<string, long> { ["installs"] = 2 }),
        ]);

        rows.Should().ContainSingle(r => r.Country == StoreMetricRules.AllCountries).Which.Downloads.Should().Be(5);
        rows.Single(r => r.Country == "TR").Should().Match<StoreMetricRow>(r => r.Impressions == 100 && r.PageViews == 10 && r.Downloads == 3);

        var parsed = ChromeWebStoreClient.ParseMetricRow(JsonNode.Parse("""{"date":{"year":2026,"month":11,"day":20},"dimensionValues":["TR"],"metricValues":[{"metric":"installs","value":"3"}]}"""));

        parsed.Should().BeEquivalentTo(new ChromeMetricRead(day, "TR", new Dictionary<string, long> { ["installs"] = 3 }));
        ChromeWebStoreClient.ParseReview(JsonNode.Parse("""{"name":"reviews/abc","rating":4,"comment":"Nice","createTime":"2026-11-20T10:00:00Z","authorInfo":{"displayName":"Ayse"}}"""))
                            .Should().Match<StoreReviewRecord>(r => r.ExternalId == "abc" && r.Rating == 4 && r.Body == "Nice" && r.AuthorName == "Ayse");
    }

    [Fact]
    public void UnsupportedDimension_ShouldOnlyMatchTheStoresDimensionRefusal()
    {
        ChromeChannel.IsUnsupportedDimension(new StoreCallException("400: Invalid format for 'dimensions': 'country'.", 400, false)).Should().BeTrue();
        ChromeChannel.IsUnsupportedDimension(new StoreCallException("400: Invalid value for 'metrics'.", 400, false)).Should().BeFalse();
        ChromeChannel.IsUnsupportedDimension(new StoreCallException("403: Permission denied (dimensions).", 403, false)).Should().BeFalse();
        ChromeChannel.IsUnsupportedDimension(new StoreCallException("400: dimensions", 400, true)).Should().BeFalse();
    }

    [Fact]
    public void Channel_ShouldBeInTheChannelTable()
    {
        var channel = StoreChannels.Of(StoreKind.Chrome);

        channel.Key.Should().Be("chrome");
        channel.HasReviews.Should().BeTrue();
        channel.HasMetrics.Should().BeTrue();
        channel.LogoIcon.Should().Be(StoreConnectionIcons.ChromeWebStore);
        Storeasy.Domain.Intake.IntakeRules.Classify("tabs-1.0.zip").Should().Be((ArtifactPlatform.Extension, PackageKind.ExtensionZip));
        Storeasy.Domain.Intake.IntakeRules.StoreOf(ArtifactPlatform.Extension).Should().Be(StoreKind.Chrome);
    }
}
