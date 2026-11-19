using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Package intake to the Microsoft Store: an MSIX delivered upload only becomes a storeasy draft (copied listings, the new package, release notes), never committed; a retry reuses the draft.")]
public class MsIntakeDeliveryTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private const string _publisher = "CN=934D5176-C6E5-455B-9AAC-3ECA3C954CB4";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private FakeMicrosoftStore Microsoft => _factory.MicrosoftStore;

    private sealed record Windows(Org Org, string ProductId, int AppId, int StoreAppId);

    private async Task<Windows> WindowsAppAsync()
    {
        var org = await NewOrgAsync();
        var tenant = Guid.NewGuid().ToString();
        var secret = JsonSerializer.Serialize(new { tenantId = tenant, clientId = "0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12", clientSecret = "client-secret-value", sellerId = "40291187" });
        var (connectionHttp, connection) = await org.Owner.Client.PostAsync<int>("storeconnections/connection", new
        {
            organizationId = org.OrganizationId,
            store = StoreKind.Microsoft,
            name = "Partner Center",
            secret,
            credentialExpiryDate = DateTime.UtcNow.AddMonths(12),
            icon = "msstore",
        });

        connectionHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        var productId = $"9N{Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L)}";
        var published = FakeMicrosoftStore.AppSubmission("Kabuk description", version: "1.0.11.0");

        published["listings"]!["en-us"]!["baseListing"]!["releaseNotes"] = "Notes of 1.0.11";
        published["listings"]!["tr-tr"] = new JsonObject
        {
            ["baseListing"] = new JsonObject { ["title"] = "Kabuk", ["description"] = "Kabuk aciklamasi", ["images"] = new JsonArray(), ["copyrightAndTrademarkInfo"] = "Milvasoft" },
            ["platformOverrides"] = new JsonObject(),
        };
        published["allowTargetFutureDeviceFamilies"] = new JsonObject { ["Desktop"] = true };

        var app = Microsoft.AddApp(tenant, productId, "Kabuk", published);

        app.Resource["packageFamilyName"] = MsixIdentity.FamilyName("Milvasoft.Kabuk", _publisher);

        var (syncHttp, jobs) = await org.Owner.Client.PostAsync<List<long>>("apps/sync", new { organizationId = org.OrganizationId, connectionId = connection.Data });

        syncHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var jobId in jobs.Data)
            (await WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        var record = await _factory.DbAsync(db => db.StoreApps.Where(s => s.OrganizationId == org.OrganizationId && s.ExternalId == productId).Select(s => new { s.Id, s.AppId }).SingleAsync());
        var (ruleHttp, ruleBody) = await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = org.OrganizationId,
            appId = record.AppId,
            rules = new[] { new { platform = ArtifactPlatform.Windows, enabled = true, target = DeliveryTarget.None, targetId = (string)null, targetName = (string)null, onWarning = DeliveryWarningAction.Deliver } },
        });

        ruleHttp.StatusCode.Should().Be(HttpStatusCode.OK, ruleBody?.MessagesToString());

        return new Windows(org, productId, record.AppId, record.Id);
    }

    private async Task<StoreJob> WaitForJobAsync(long jobId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

            if (StoreJobRules.IsFinished(job.Status))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status}.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private static List<JsonObject> Packages(JsonObject submission) => [.. submission["applicationPackages"]!.AsArray().OfType<JsonObject>()];

    private static string FileStatus(JsonObject package) => package["fileStatus"]!.GetValue<string>();

    private static string FileName(JsonObject package) => package["fileName"]!.GetValue<string>();

    private int CallCount(string method, string pathSuffix) => Microsoft.Calls.Count(c => c.StartsWith($"{method} manage.devcenter.microsoft.com", StringComparison.Ordinal) && c.EndsWith(pathSuffix, StringComparison.Ordinal));

    private int CommitCount(string productId) => Microsoft.Calls.Count(c => c.StartsWith("POST manage.devcenter.microsoft.com", StringComparison.Ordinal) && c.Contains($"/applications/{productId}/", StringComparison.Ordinal) && c.EndsWith("/commit", StringComparison.Ordinal));

    [Fact]
    public async Task UploadOnlyMsix_ShouldBecomeADraftWithTheCopiedListingsTheNewPackageAndTheNotes_NeverCommitted()
    {
        // Arrange
        var windows = await WindowsAppAsync();
        var client = TokenClient(await PatAsync(windows.Org.Owner, windows.Org.OrganizationId));
        var msix = PackageFactory.Msix("Milvasoft.Kabuk", "1.0.15.0", _publisher);
        var publishedId = Microsoft.Apps[windows.ProductId].PublishedId;
        var published = Microsoft.Submissions[publishedId].DeepClone().AsObject();

        // Act
        var (sessionId, _, _) = await UploadAsync(client, windows.AppId, "Kabuk_1.0.15.0_x64.msix", msix, new { notes = "Faster start" });
        var session = await WaitForSessionAsync(sessionId);

        // Assert
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);

        var draftId = Microsoft.Apps[windows.ProductId].PendingId;

        draftId.Should().NotBeNull();
        draftId.Should().NotBe(publishedId);
        session.StoreBuildId.Should().Be(draftId, "the draft is recorded on the session so a member can finish it");
        session.Steps.Single(s => s.Kind == ArtifactStepKind.Deliver).Message.Should().Contain(draftId);

        var draft = Microsoft.Submissions[draftId];

        draft["status"]!.GetValue<string>().Should().Be("PendingCommit", "upload only never sends the draft to certification");
        CommitCount(windows.ProductId).Should().Be(0);
        CallCount("DELETE", draftId).Should().Be(0);

        // Every copied listing, image, price and unknown field went back unchanged; only the release notes are new.
        var listings = draft["listings"]!.AsObject();

        listings.Select(l => l.Key).Should().BeEquivalentTo("en-us", "tr-tr");
        listings["en-us"]!["baseListing"]!["title"]!.GetValue<string>().Should().Be("Pusula");
        listings["en-us"]!["baseListing"]!["description"]!.GetValue<string>().Should().Be("Kabuk description");
        listings["en-us"]!["baseListing"]!["images"]!.ToJsonString().Should().Be(published["listings"]!["en-us"]!["baseListing"]!["images"]!.ToJsonString());
        listings["tr-tr"]!["baseListing"]!["copyrightAndTrademarkInfo"]!.GetValue<string>().Should().Be("Milvasoft");
        listings["en-us"]!["baseListing"]!["releaseNotes"]!.GetValue<string>().Should().Be("Faster start");
        listings["tr-tr"]!["baseListing"]!["releaseNotes"]!.GetValue<string>().Should().Be("Faster start");
        draft["pricing"]!.ToJsonString().Should().Be(published["pricing"]!.ToJsonString());
        draft["allowTargetFutureDeviceFamilies"]!.ToJsonString().Should().Be(published["allowTargetFutureDeviceFamilies"]!.ToJsonString());

        // The published package stays; the new one waits for its file, which is in the uploaded ZIP.
        var packages = Packages(draft);

        packages.Should().HaveCount(2);
        packages.Should().ContainSingle(p => FileName(p) == "app_1.0.11.0_x64.msix" && FileStatus(p) == "Uploaded");

        var added = packages.Single(p => FileStatus(p) == "PendingUpload");

        FileName(added).Should().StartWith("packages/").And.EndWith("_Kabuk_1.0.15.0_x64.msix");
        Microsoft.UploadedEntries(draftId).Should().BeEquivalentTo([FileName(added)]);

        var row = await _factory.DbAsync(db => db.MsSubmissions.AsNoTracking().SingleAsync(s => s.StoreAppId == windows.StoreAppId && s.ExternalId == draftId));

        row.CreatedByStoreasy.Should().BeTrue();
        row.IsPending.Should().BeTrue();
    }

    [Fact]
    public async Task UploadOnlyMsix_WithoutNotes_ShouldKeepTheCopiedReleaseNotes()
    {
        // Arrange
        var windows = await WindowsAppAsync();
        var client = TokenClient(await PatAsync(windows.Org.Owner, windows.Org.OrganizationId));

        // Act
        var (sessionId, _, _) = await UploadAsync(client, windows.AppId, "Kabuk_1.0.15.0_x64.msix", PackageFactory.Msix("Milvasoft.Kabuk", "1.0.15.0", _publisher));
        var session = await WaitForSessionAsync(sessionId);

        // Assert
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);

        var draft = Microsoft.Submissions[Microsoft.Apps[windows.ProductId].PendingId];

        draft["listings"]!["en-us"]!["baseListing"]!["releaseNotes"]!.GetValue<string>().Should().Be("Notes of 1.0.11");
        draft["listings"]!["tr-tr"]!["baseListing"]!.AsObject().ContainsKey("releaseNotes").Should().BeFalse();
    }

    [Fact]
    public async Task Retry_ShouldReuseThePendingDraftStoreasyCreated_WithOneEntryForThePackage()
    {
        // Arrange: the first delivery creates the draft and fails on the file upload.
        var windows = await WindowsAppAsync();
        var client = TokenClient(await PatAsync(windows.Org.Owner, windows.Org.OrganizationId));
        var msix = PackageFactory.Msix("Milvasoft.Kabuk", "1.0.15.0", _publisher);

        Microsoft.FailUploads = HttpStatusCode.Forbidden;

        StoreasyArtifactSessionSnapshot first;

        try
        {
            var (firstId, _, _) = await UploadAsync(client, windows.AppId, "Kabuk_1.0.15.0_x64.msix", msix, new { notes = "Faster start" });

            first = new StoreasyArtifactSessionSnapshot(await WaitForSessionAsync(firstId), Microsoft.Apps[windows.ProductId].PendingId);
        }
        finally
        {
            Microsoft.FailUploads = null;
        }

        var createsBefore = CallCount("POST", $"applications/{windows.ProductId}/submissions");

        // Act: the same file again.
        var (secondId, _, _) = await UploadAsync(client, windows.AppId, "Kabuk_1.0.15.0_x64.msix", msix, new { notes = "Faster start" });
        var second = await WaitForSessionAsync(secondId);

        // Assert
        first.Session.Status.Should().Be(ArtifactSessionStatus.Failed, first.Session.StatusDescription);
        first.DraftId.Should().NotBeNull("the draft was created before the upload failed");
        second.Status.Should().Be(ArtifactSessionStatus.Delivered, second.StatusDescription);
        Microsoft.Apps[windows.ProductId].PendingId.Should().Be(first.DraftId, "the pending draft storeasy created is reused");
        second.StoreBuildId.Should().Be(first.DraftId);
        CallCount("POST", $"applications/{windows.ProductId}/submissions").Should().Be(createsBefore, "no second draft is created");
        CallCount("DELETE", first.DraftId).Should().Be(0);
        CommitCount(windows.ProductId).Should().Be(0);

        var draft = Microsoft.Submissions[first.DraftId];
        var pending = Packages(draft).Where(p => FileStatus(p) == "PendingUpload").ToList();

        pending.Should().ContainSingle("the entry of the failed attempt is replaced, not duplicated");
        Packages(draft).Should().ContainSingle(p => FileName(p) == "app_1.0.11.0_x64.msix" && FileStatus(p) == "Uploaded");
        Microsoft.UploadedEntries(first.DraftId).Should().BeEquivalentTo([FileName(pending[0])]);
        draft["listings"]!["en-us"]!["baseListing"]!["releaseNotes"]!.GetValue<string>().Should().Be("Faster start");
        draft["status"]!.GetValue<string>().Should().Be("PendingCommit");
    }

    [Fact]
    public async Task PendingDraftMadeInPartnerCenter_ShouldFailTheDelivery_AndNeverBeDeleted()
    {
        // Arrange
        var windows = await WindowsAppAsync();
        var client = TokenClient(await PatAsync(windows.Org.Owner, windows.Org.OrganizationId));
        var foreignId = Microsoft.AddPartnerCenterDraft(windows.ProductId);
        var before = Microsoft.Submissions[foreignId].ToJsonString();

        // Act
        var (sessionId, _, _) = await UploadAsync(client, windows.AppId, "Kabuk_1.0.15.0_x64.msix", PackageFactory.Msix("Milvasoft.Kabuk", "1.0.15.0", _publisher));
        var session = await WaitForSessionAsync(sessionId);

        // Assert
        session.Status.Should().Be(ArtifactSessionStatus.Failed);
        Microsoft.Apps[windows.ProductId].PendingId.Should().Be(foreignId);
        Microsoft.Submissions[foreignId].ToJsonString().Should().Be(before, "a draft storeasy did not create is neither changed nor deleted");
        CallCount("DELETE", foreignId).Should().Be(0);
        CallCount("PUT", foreignId).Should().Be(0);
    }

    private sealed record StoreasyArtifactSessionSnapshot(ArtifactSession Session, string DraftId);
}
