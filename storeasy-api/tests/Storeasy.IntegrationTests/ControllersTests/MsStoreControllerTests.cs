using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Microsoft Store: connection, sync, plan and apply, submit, flights, add-ons, MSI/EXE, isolation; against a fake Microsoft.")]
public class MsStoreControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private FakeMicrosoftStore Microsoft => _factory.MicrosoftStore;

    private sealed record Setup(TestUser Owner, int OrganizationId, int ConnectionId, string Tenant, string AppId);

    private static string Secret(string tenant, string clientSecret = "client-secret-value") => JsonSerializer.Serialize(new { tenantId = tenant, clientId = "0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12", clientSecret, sellerId = "40291187" });

    private static string NewProductId() => $"9N{Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L)}";

    private async Task<Setup> NewSetupAsync(bool withApp = true)
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var tenant = Guid.NewGuid().ToString();
        var (http, connection) = await owner.Client.PostAsync<int>("storeconnections/connection", new
        {
            organizationId = organization.Data,
            store = StoreKind.Microsoft,
            name = "Partner Center",
            secret = Secret(tenant),
            credentialExpiryDate = DateTime.UtcNow.AddMonths(12),
            icon = "msstore",
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        var appId = NewProductId();

        if (withApp)
            Microsoft.AddApp(tenant, appId, "Pusula", FakeMicrosoftStore.AppSubmission("Old description"));

        return new Setup(owner, organization.Data, connection.Data, tenant, appId);
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

    private async Task SyncAsync(Setup setup)
    {
        var (http, jobs) = await setup.Owner.Client.PostAsync<List<long>>("apps/sync", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var jobId in jobs.Data)
        {
            var job = await WaitForJobAsync(jobId);

            job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
            job.Kind.Should().Be(StoreJobKind.MsSync);
        }
    }

    private Task<int> StoreAppIdAsync(Setup setup, string productId)
        => _factory.DbAsync(db => db.StoreApps.Where(s => s.OrganizationId == setup.OrganizationId && s.ExternalId == productId).Select(s => s.Id).SingleAsync());

    private async Task<MsChangePlanDto> PlanAsync(Setup setup, int storeAppId, object change)
    {
        var (http, plan) = await setup.Owner.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change, dryRun = true });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", plan.Messages?.Select(m => m.Message) ?? []));

        return plan.Data;
    }

    private async Task<StoreJob> ApplyAsync(Setup setup, int storeAppId, object change, TestUser user = null)
    {
        var plan = await PlanAsync(setup, storeAppId, change);
        var (http, applied) = await (user ?? setup.Owner).Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change, dryRun = false, baseHash = plan.BaseHash });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", applied.Messages?.Select(m => m.Message) ?? []));
        applied.Data.JobId.Should().NotBeNull();

        return await WaitForJobAsync(applied.Data.JobId.Value);
    }

    private async Task<TestUser> AddMemberAsync(Setup setup, OrgRole role)
    {
        var user = await _factory.CreateUserAsync(email: $"m{TestApi.Unique()}@example.com");
        var member = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await setup.Owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = setup.OrganizationId, email = user.Email, role });

        await member.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        return member;
    }

    private static object ListingChange(string description) => new { kind = MsChangeKind.Listings, listings = new[] { new { locale = "en-us", description } } };

    [Fact]
    public async Task Connection_ShouldKeepTheFingerprintAndExpiry_AndRotateOnlyAVerifiedSecretOfTheSameApp()
    {
        // Arrange
        var setup = await NewSetupAsync(withApp: false);

        // Act
        var (_, list) = await setup.Owner.Client.GetAsync<List<StoreConnectionDto>>($"storeconnections?organizationId={setup.OrganizationId}");
        var (_, expiringSoon) = await setup.Owner.Client.GetAsync<List<MsExpiringConnectionDto>>($"ms/connections/expiring?organizationId={setup.OrganizationId}&days=30");
        var (_, expiringYear) = await setup.Owner.Client.GetAsync<List<MsExpiringConnectionDto>>($"ms/connections/expiring?organizationId={setup.OrganizationId}&days=400");
        var (mismatch, mismatchBody) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("ms/connection/secret", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(Guid.NewGuid().ToString()), credentialExpiryDate = DateTime.UtcNow.AddMonths(12) });
        var (tooLong, _) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("ms/connection/secret", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(setup.Tenant, "new"), credentialExpiryDate = DateTime.UtcNow.AddMonths(30) });
        var newExpiry = DateTime.UtcNow.AddMonths(20);
        var (rotated, rotatedBody) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("ms/connection/secret", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(setup.Tenant, "rotated-secret"), credentialExpiryDate = newExpiry });

        // Assert
        var connection = list.Data.Single();

        connection.Fingerprint.TenantIdSuffix.Should().Be(setup.Tenant[^4..]);
        connection.Fingerprint.ClientIdSuffix.Should().Be("be12");
        connection.Fingerprint.SellerId.Should().Be("40291187");
        connection.Icon.Should().Be("msstore");
        connection.CredentialExpiryDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(12), TimeSpan.FromMinutes(5));
        JsonSerializer.Serialize(list.Data).Should().NotContain("client-secret-value");
        expiringSoon.Data.Should().BeEmpty();
        expiringYear.Data.Should().ContainSingle().Which.DaysLeft.Should().BeInRange(360, 366);
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        mismatchBody.Messages.Should().Contain(m => m.Key == MessageKey.MsSecretMismatch);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        rotatedBody.Data.CredentialExpiryDate.Should().BeCloseTo(newExpiry, TimeSpan.FromSeconds(1));
        rotatedBody.Data.Icon.Should().Be("msstore", "replacing the secret keeps the icon");
        _factory.StoreGateway.VerifiedSecrets.Should().Contain(s => s.Contains("rotated-secret"));
    }

    [Fact]
    public async Task SecretExpiry_ShouldBeRemindedOncePerThreshold_ByTheDeadlineNotifier()
    {
        // Arrange
        var setup = await NewSetupAsync(withApp: false);
        var expiry = DateTime.UtcNow.AddDays(20);

        await _factory.DbAsync(db => db.StoreConnections.Where(c => c.Id == setup.ConnectionId).ExecuteUpdateAsync(s => s.SetProperty(c => c.SecretExpiryDate, expiry)));

        var now = DateTime.UtcNow;

        // Act
        await _factory.WithServiceAsync<Storeasy.Application.Interfaces.IDeadlineNotifier, int>(n => n.NotifyMsSecretExpiryAsync(now, CancellationToken.None));
        await _factory.WithServiceAsync<Storeasy.Application.Interfaces.IDeadlineNotifier, int>(n => n.NotifyMsSecretExpiryAsync(now.AddHours(1), CancellationToken.None));
        await _factory.WithServiceAsync<Storeasy.Application.Interfaces.IDeadlineNotifier, int>(n => n.NotifyMsSecretExpiryAsync(now.AddDays(14), CancellationToken.None));

        var reminders = await _factory.DbAsync(db => db.DeadlineReminders.AsNoTracking().Where(r => r.Subject.StartsWith($"ms-secret:{setup.ConnectionId}:")).Select(r => r.ThresholdDays).ToListAsync());

        // Assert
        reminders.Should().BeEquivalentTo([30, 7], "one reminder per threshold, never twice");
    }

    [Fact]
    public async Task Sync_ShouldBringAppsSubmissionsFlightsAddonsAndPackages()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var second = NewProductId();

        Microsoft.AddApp(setup.Tenant, second, "Tarla", null);
        Microsoft.AddFlight(setup.AppId, "flight-1", "Kuzey ici", FakeMicrosoftStore.AppSubmission("flight", version: "2.0.0.0"));
        Microsoft.AddAddon(setup.AppId, $"9A{TestApi.Unique()}", "coins", "Coins");

        // Act
        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var (_, product) = await setup.Owner.Client.GetAsync<MsProductDto>($"ms/product?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var (_, submissions) = await setup.Owner.Client.GetAsync<List<MsSubmissionDto>>($"ms/submissions?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var (_, flights) = await setup.Owner.Client.GetAsync<List<MsFlightDto>>($"ms/flights?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var (_, addons) = await setup.Owner.Client.GetAsync<List<MsAddonDto>>($"ms/addons?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var builds = await _factory.DbAsync(db => db.Builds.Where(b => b.StoreAppId == storeAppId).ToListAsync());
        var secondProduct = await _factory.DbAsync(db => db.MsProducts.SingleAsync(p => p.StoreApp.ExternalId == second && p.OrganizationId == setup.OrganizationId));

        // Assert
        product.Data.Kind.Should().Be(MsProductKind.Msix);
        product.Data.FirstSubmissionDone.Should().BeTrue();
        product.Data.PackageFamilyName.Should().Contain(setup.AppId);
        product.Data.Notices.Should().HaveCount(2);
        submissions.Data.Should().Contain(s => s.Target == MsSubmissionTarget.App && s.Status == MsSubmissionStatus.Published && !s.IsPending);
        submissions.Data.Should().Contain(s => s.Target == MsSubmissionTarget.Flight && s.IsPending && s.FlightId == "flight-1");
        submissions.Data.Should().Contain(s => s.Target == MsSubmissionTarget.Addon && s.Status == MsSubmissionStatus.Published);
        flights.Data.Should().ContainSingle().Which.Should().Match<MsFlightDto>(f => f.FriendlyName == "Kuzey ici" && f.Groups.Count == 1);
        addons.Data.Should().ContainSingle().Which.Should().Match<MsAddonDto>(a => a.ProductId == "coins" && a.Title == "Coins" && a.ApiWritable && a.PriceId == "Tier2");
        builds.Select(b => b.Version).Should().Contain(["1.0.0.0", "2.0.0.0"]);
        secondProduct.FirstSubmissionDone.Should().BeFalse("an app without a published submission needs its first submission in Partner Center");
    }

    [Fact]
    public async Task Listings_ShouldPlanApplyIntoAStoreasyDraft_AndRefuseAStaleHash()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var stalePlan = await PlanAsync(setup, storeAppId, ListingChange("Stale"));

        // Act
        var plan = await PlanAsync(setup, storeAppId, ListingChange("New description"));
        var job = await ApplyAsync(setup, storeAppId, ListingChange("New description"));
        var (stale, staleBody) = await setup.Owner.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change = ListingChange("Stale"), dryRun = false, baseHash = stalePlan.BaseHash });
        var (tooLong, _) = await setup.Owner.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change = ListingChange(new string('a', 10001)) });

        // Assert
        plan.CreatesSubmission.Should().BeTrue();
        plan.Items.Should().ContainSingle(i => i.Field == "description" && i.Before == "Old description" && i.After == "New description");
        plan.Warnings.Should().OnlyContain(w => !string.IsNullOrEmpty(w.Message));
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);

        var draftId = Microsoft.Apps[setup.AppId].PendingId;

        draftId.Should().NotBeNull();
        Microsoft.Submissions[draftId]["listings"]!["en-us"]!["baseListing"]!["description"]!.GetValue<string>().Should().Be("New description");
        Microsoft.Submissions[draftId]["status"]!.GetValue<string>().Should().Be("PendingCommit", "nothing is sent to certification without submit");

        var row = await _factory.DbAsync(db => db.MsSubmissions.SingleAsync(s => s.StoreAppId == storeAppId && s.ExternalId == draftId));

        row.CreatedByStoreasy.Should().BeTrue();
        row.ContentHash.Should().NotBeNullOrEmpty();
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        staleBody.Messages.Should().Contain(m => m.Key == MessageKey.MsPlanStale);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Packages_ShouldUploadAZipOfTheAssetsToTheDraft_AndSubmitNeedsConfirmationAndRole()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var asset = new Asset { OrganizationId = setup.OrganizationId, Purpose = AssetPurpose.BuildArtifact, ObjectKey = $"org/{setup.OrganizationId}/pusula_1.1.0.0_x64.msix", FileName = "pusula_1.1.0.0_x64.msix", ContentType = "application/msix", Size = 10, Status = AssetStatus.Uploaded };
        var image = new Asset { OrganizationId = setup.OrganizationId, Purpose = AssetPurpose.Screenshot, ObjectKey = $"org/{setup.OrganizationId}/shot2.png", FileName = "shot2.png", ContentType = "image/png", Size = 10, Status = AssetStatus.Uploaded };

        await _factory.DbAsync(async db =>
        {
            db.Assets.AddRange(asset, image);
            await db.SaveChangesAsync();
        });

        var editor = await AddMemberAsync(setup, OrgRole.Editor);

        // Act
        var packagesJob = await ApplyAsync(setup, storeAppId, new { kind = MsChangeKind.Packages, packages = new { addAssetIds = new[] { asset.Id }, removeFileNames = new[] { "app_1.0.0.0_x64.msix" }, rolloutPercentage = 20 } });
        var imagesJob = await ApplyAsync(setup, storeAppId, new { kind = MsChangeKind.Images, images = new { locale = "en-us", add = new[] { new { assetId = image.Id, imageType = "Screenshot", description = "Map" } } } });
        var draftId = Microsoft.Apps[setup.AppId].PendingId;
        var (editorSubmit, _) = await editor.Client.PostAsync<long>("ms/submission/submit", new { organizationId = setup.OrganizationId, storeAppId, target = MsSubmissionTarget.App, confirm = setup.AppId });
        var (wrongConfirm, wrongConfirmBody) = await setup.Owner.Client.PostAsync<long>("ms/submission/submit", new { organizationId = setup.OrganizationId, storeAppId, target = MsSubmissionTarget.App, confirm = "nope" });
        var (submitted, submitJob) = await setup.Owner.Client.PostAsync<long>("ms/submission/submit", new { organizationId = setup.OrganizationId, storeAppId, target = MsSubmissionTarget.App, confirm = setup.AppId });
        var submitResult = await WaitForJobAsync(submitJob.Data);

        // Assert
        packagesJob.Status.Should().Be(StoreJobStatus.Succeeded, packagesJob.Error);
        imagesJob.Status.Should().Be(StoreJobStatus.Succeeded, imagesJob.Error);
        Microsoft.UploadedEntries(draftId).Should().Contain($"images/{image.Id}_shot2.png");

        var packages = Microsoft.Submissions[draftId]["applicationPackages"]!.AsArray();

        packages.Should().Contain(p => p!["fileName"]!.GetValue<string>() == $"packages/{asset.Id}_pusula_1.1.0.0_x64.msix");
        packages.Should().Contain(p => p!["fileName"]!.GetValue<string>() == "app_1.0.0.0_x64.msix" && p["fileStatus"]!.GetValue<string>() == "PendingDelete");
        Microsoft.Submissions[draftId]["packageDeliveryOptions"]!["packageRollout"]!["packageRolloutPercentage"]!.GetValue<double>().Should().Be(20);
        editorSubmit.StatusCode.Should().Be(HttpStatusCode.Forbidden, "submitting needs SubmitForReview");
        wrongConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        wrongConfirmBody.Messages.Should().Contain(m => m.Key == MessageKey.MsConfirmationMismatch);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        submitResult.Status.Should().Be(StoreJobStatus.Succeeded, submitResult.Error);

        var row = await _factory.DbAsync(db => db.MsSubmissions.AsNoTracking().SingleAsync(s => s.StoreAppId == storeAppId && s.ExternalId == draftId));

        row.Status.Should().Be(MsSubmissionStatus.PreProcessing);
        row.StatusHistory.Select(h => h.Status).Should().ContainInOrder(MsSubmissionStatus.PendingCommit, MsSubmissionStatus.PreProcessing);
    }

    [Fact]
    public async Task ADraftChangedInPartnerCenter_ShouldBeFlagged_AndNeitherEditedNorSubmitted()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);

        (await ApplyAsync(setup, storeAppId, ListingChange("Mine"))).Status.Should().Be(StoreJobStatus.Succeeded);

        var draftId = Microsoft.Apps[setup.AppId].PendingId;

        Microsoft.EditInPartnerCenter(draftId);

        // Act
        var plan = await PlanAsync(setup, storeAppId, ListingChange("Again"));

        await SyncAsync(setup);

        var (_, product) = await setup.Owner.Client.GetAsync<MsProductDto>($"ms/product?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var (submit, submitBody) = await setup.Owner.Client.PostAsync<long>("ms/submission/submit", new { organizationId = setup.OrganizationId, storeAppId, confirm = setup.AppId });
        var discard = await ApplyAsync(setup, storeAppId, new { kind = MsChangeKind.DiscardSubmission });

        // Assert
        plan.Blocked.Should().BeTrue();
        plan.BlockKey.Should().Be(MessageKey.MsSubmissionModifiedOutsideApi);
        plan.AssistedReason.Should().Be(MsAssistedReason.ManualEdit);
        product.Data.ActiveSubmission.ModifiedOutsideApi.Should().BeTrue();
        product.Data.AssistedSteps.Should().Contain(s => s.Key == "ms.submission.manual-edit" && s.Pending);
        submit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        submitBody.Messages.Should().Contain(m => m.Key == MessageKey.MsSubmissionModifiedOutsideApi);
        discard.Status.Should().Be(StoreJobStatus.Succeeded, discard.Error);
        Microsoft.Apps[setup.AppId].PendingId.Should().BeNull("the changed draft can only be deleted");
    }

    [Fact]
    public async Task OnlyOneActiveSubmission_ShouldBlockChanges_WhileOneIsInCertification()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);

        (await ApplyAsync(setup, storeAppId, ListingChange("First"))).Status.Should().Be(StoreJobStatus.Succeeded);

        var draftId = Microsoft.Apps[setup.AppId].PendingId;

        Microsoft.Submissions[draftId]["status"] = "Certification";

        // Act
        var plan = await PlanAsync(setup, storeAppId, ListingChange("Second"));
        var (apply, applyBody) = await setup.Owner.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change = ListingChange("Second"), dryRun = false, baseHash = plan.BaseHash });

        // Assert
        plan.Blocked.Should().BeTrue();
        plan.BlockKey.Should().Be(MessageKey.MsSubmissionInProgress);
        apply.StatusCode.Should().Be(HttpStatusCode.Conflict);
        applyBody.Messages.Should().Contain(m => m.Key == MessageKey.MsSubmissionInProgress);
    }

    [Fact]
    public async Task FlightsAddonsAndRollout_ShouldBeCreatedAndChanged_WithTheirOwnPermissions()
    {
        // Arrange
        var setup = await NewSetupAsync();
        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var publishedId = Microsoft.Apps[setup.AppId].PublishedId;

        Microsoft.Submissions[publishedId]["packageDeliveryOptions"]!["packageRollout"] = new System.Text.Json.Nodes.JsonObject { ["isPackageRollout"] = true, ["packageRolloutPercentage"] = 10.0, ["packageRolloutStatus"] = "PackageRolloutInProgress", ["fallbackSubmissionId"] = "0" };

        var editor = await AddMemberAsync(setup, OrgRole.Editor);
        var rollout = new { kind = MsChangeKind.Rollout, rollout = new { submissionId = publishedId, action = MsRolloutAction.SetPercentage, percentage = 50 } };

        // Act
        var flightJob = await ApplyAsync(setup, storeAppId, new { kind = MsChangeKind.CreateFlight, flight = new { friendlyName = "Early Windows", groupIds = new[] { "1152921504606962205" } } }, editor);
        var addonJob = await ApplyAsync(setup, storeAppId, new { kind = MsChangeKind.CreateAddon, addon = new { productId = "premium", productType = MsAddonType.Durable } }, editor);
        var editorPlan = await PlanAsync(setup, storeAppId, rollout);
        var (editorRollout, _) = await editor.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change = rollout, dryRun = false, baseHash = editorPlan.BaseHash });
        var rolloutJob = await ApplyAsync(setup, storeAppId, rollout);
        var duplicate = await PlanAsync(setup, storeAppId, new { kind = MsChangeKind.CreateFlight, flight = new { friendlyName = "early windows", groupIds = new[] { "1" } } });

        // Assert
        flightJob.Status.Should().Be(StoreJobStatus.Succeeded, flightJob.Error);
        addonJob.Status.Should().Be(StoreJobStatus.Succeeded, addonJob.Error);
        (await _factory.DbAsync(db => db.MsFlights.CountAsync(f => f.StoreAppId == storeAppId && f.FriendlyName == "Early Windows"))).Should().Be(1);
        (await _factory.DbAsync(db => db.MsAddons.CountAsync(a => a.StoreAppId == storeAppId && a.ProductId == "premium"))).Should().Be(1);
        editorPlan.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        editorRollout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rolloutJob.Status.Should().Be(StoreJobStatus.Succeeded, rolloutJob.Error);
        Microsoft.Submissions[publishedId]["packageDeliveryOptions"]!["packageRollout"]!["packageRolloutPercentage"]!.GetValue<double>().Should().Be(50);
        duplicate.BlockKey.Should().Be(MessageKey.MsFlightNameTaken);
    }

    [Fact]
    public async Task AddonThatTheApiRefuses_ShouldBecomeAnAssistedStep()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var addonId = $"9A{TestApi.Unique()}";
        var addon = Microsoft.AddAddon(setup.AppId, addonId, "gems", "Gems");

        Microsoft.Conflicts[$"inappproducts/{addonId}/submissions/{addon.PublishedId}"] = true;

        // Act
        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var (_, addons) = await setup.Owner.Client.GetAsync<List<MsAddonDto>>($"ms/addons?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var plan = await PlanAsync(setup, storeAppId, new { kind = MsChangeKind.AddonContent, addonStoreId = addonId, addonContent = new { priceId = "Tier3" } });
        var (_, product) = await setup.Owner.Client.GetAsync<MsProductDto>($"ms/product?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");

        // Assert
        addons.Data.Single(a => a.StoreId == addonId).Should().Match<MsAddonDto>(a => !a.ApiWritable && a.AssistedReason == MsAssistedReason.StoreManagedConsumable);
        plan.Blocked.Should().BeTrue();
        plan.BlockKey.Should().Be(MessageKey.MsAddonAssisted);
        product.Data.AssistedSteps.Should().Contain(s => s.Key == "ms.addon.assisted-update" && s.Pending && s.StepId != null && s.Status == AssistedStepStatus.Todo);

        var appId = await _factory.DbAsync(db => db.StoreApps.Where(s => s.Id == storeAppId).Select(s => s.AppId).SingleAsync());
        var (_, steps) = await setup.Owner.Client.PatchAsync<List<Storeasy.Application.Dtos.AssistedStepDtos.AssistedStepListDto>>("assisted-steps", new { organizationId = setup.OrganizationId, appId, store = StoreKind.Microsoft });

        steps.Data.Should().Contain(s => s.Key == "ms.addon.assisted-update" && !string.IsNullOrEmpty(s.Title) && !s.Title.StartsWith("AssistedStep."), "the refusal is persisted as an assisted step with its texts");
        steps.Data.Should().Contain(s => s.Key == "ms.reviews.reply");
    }

    [Fact]
    public async Task ADryRunPlan_ShouldBeAChangePlan_ApprovedThroughTheSharedPlanEndpoints()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var editor = await AddMemberAsync(setup, OrgRole.Editor);
        var plan = await PlanAsync(setup, storeAppId, ListingChange("Approved later"));

        // Act
        var (_, saved) = await setup.Owner.Client.GetAsync<Storeasy.Application.Dtos.ContentDtos.ChangePlanDto>($"plans/plan?organizationId={setup.OrganizationId}&planId={plan.PlanId}");
        var (approved, approvedBody) = await editor.Client.PostAsync<Storeasy.Application.Dtos.ContentDtos.ChangePlanDto>("plans/approve", new { organizationId = setup.OrganizationId, planId = plan.PlanId });
        var job = await WaitForJobAsync(approvedBody.Data.JobId.Value);
        var plans = await _factory.DbAsync(db => db.ChangePlans.AsNoTracking().SingleAsync(p => p.Id == plan.PlanId));

        // Assert
        plan.PlanId.Should().NotBeNull();
        saved.Data.Kind.Should().Be(ChangePlanKind.MsChange);
        saved.Data.Store.Should().Be(StoreKind.Microsoft);
        saved.Data.Items.Should().ContainSingle(i => i.Field == "description" && i.After == "Approved later");
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        job.Kind.Should().Be(StoreJobKind.ApplyChangePlan);
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        plans.Status.Should().Be(ChangePlanStatus.Applied);
        Microsoft.Submissions[Microsoft.Apps[setup.AppId].PendingId]["listings"]!["en-us"]!["baseListing"]!["description"]!.GetValue<string>().Should().Be("Approved later");
    }

    [Fact]
    public async Task ApprovingAPlan_ShouldBeRefused_WhenTheStoreChangedSinceThePlan()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var plan = await PlanAsync(setup, storeAppId, ListingChange("Mine"));

        Microsoft.Submissions[Microsoft.Apps[setup.AppId].PublishedId]["listings"]!["en-us"]!["baseListing"]!["description"] = "Changed in Partner Center";

        // Act
        var (approved, approvedBody) = await setup.Owner.Client.PostAsync<Storeasy.Application.Dtos.ContentDtos.ChangePlanDto>("plans/approve", new { organizationId = setup.OrganizationId, planId = plan.PlanId });

        // Assert
        approved.StatusCode.Should().Be(HttpStatusCode.Conflict);
        approvedBody.Messages.Should().Contain(m => m.Key == MessageKey.PlanConflict);
    }

    [Fact]
    public async Task MsiExeProduct_ShouldBeAddedByIdAndUpdated_OnlyWithSignedInstallers()
    {
        // Arrange
        var setup = await NewSetupAsync(withApp: false);
        var productId = NewProductId();

        Microsoft.AddProduct(productId, ready: false);
        Microsoft.Installers["/pusula-2.0.exe"] = UnitTestsPe.Signed();
        Microsoft.Installers["/pusula-unsigned.exe"] = [(byte)'M', (byte)'Z', .. new byte[4000]];

        var (added, addedBody) = await setup.Owner.Client.PostAsync<MsProductAddedDto>("ms/product", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, productId, name = "Pusula Installer" });
        var addJob = await WaitForJobAsync(addedBody.Data.JobId);
        var storeAppId = addedBody.Data.StoreAppId;

        static object Installer(string path) => new
        {
            kind = MsChangeKind.InstallerPackages,
            installerPackages = new[] { new { packageUrl = $"https://{FakeMicrosoftStore.DownloadHost}{path}", packageType = MsInstallerType.Exe, architecture = "X64", languages = new[] { "en-us" }, isSilentInstall = false, installerParameters = "/S", genericDocUrl = "https://example.com/codes" } },
        };

        // Act
        var unsignedJob = await ApplyAsync(setup, storeAppId, Installer("/pusula-unsigned.exe"));
        var putAfterUnsigned = Microsoft.Calls.Count(c => c.StartsWith("PUT api.store.microsoft.com") && c.Contains(productId));
        var (submitEarly, submitEarlyBody) = await setup.Owner.Client.PostAsync<long>("ms/submission/submit", new { organizationId = setup.OrganizationId, storeAppId, confirm = productId });
        var signedJob = await ApplyAsync(setup, storeAppId, Installer("/pusula-2.0.exe"));
        var listingJob = await ApplyAsync(setup, storeAppId, ListingChange("Installer description"));
        var (_, packages) = await setup.Owner.Client.GetAsync<List<MsInstallerPackageDto>>($"ms/packages?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var (submit, submitBody) = await setup.Owner.Client.PostAsync<long>("ms/submission/submit", new { organizationId = setup.OrganizationId, storeAppId, confirm = productId });
        var submitJob = await WaitForJobAsync(submitBody.Data);

        // Assert
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        addJob.Status.Should().Be(StoreJobStatus.Succeeded, addJob.Error);
        unsignedJob.Status.Should().Be(StoreJobStatus.Failed);
        unsignedJob.Error.Should().Contain("Authenticode");
        putAfterUnsigned.Should().Be(0, "nothing is written while an installer is unsigned");
        submitEarly.StatusCode.Should().Be(HttpStatusCode.Conflict);
        submitEarlyBody.Messages.Should().Contain(m => m.Key == MessageKey.MsInstallerNotReady);
        signedJob.Status.Should().Be(StoreJobStatus.Succeeded, signedJob.Error);
        listingJob.Status.Should().Be(StoreJobStatus.Succeeded, listingJob.Error);
        Microsoft.Products[productId]["metadata"]!["listings"]![0]!["description"]!.GetValue<string>().Should().Be("Installer description");
        packages.Data.Should().ContainSingle().Which.Should().Match<MsInstallerPackageDto>(p => p.SignatureStatus == MsSignatureStatus.Signed && p.PackageId != null && p.InstallerParameters == "/S");
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        submitJob.Status.Should().Be(StoreJobStatus.Succeeded, submitJob.Error);
        (await _factory.DbAsync(db => db.MsSubmissions.SingleAsync(s => s.StoreAppId == storeAppId && s.Target == MsSubmissionTarget.Installer))).Status.Should().Be(MsSubmissionStatus.InProgress);
    }

    [Fact]
    public async Task MsiExeProduct_TheStoreRefuses_ShouldBeRemovedBySync()
    {
        // Arrange
        var setup = await NewSetupAsync(withApp: false);
        var productId = NewProductId();

        // Act
        var (_, added) = await setup.Owner.Client.PostAsync<MsProductAddedDto>("ms/product", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, productId });
        var job = await WaitForJobAsync(added.Data.JobId);
        var (again, _) = await setup.Owner.Client.GetAsync<MsProductDto>($"ms/product?organizationId={setup.OrganizationId}&storeAppId={added.Data.StoreAppId}");

        // Assert
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sync_ShouldStoreThePackageFamilyNameOnTheRecord_AndRefreshItOnEverySync()
    {
        // Arrange: an MSIX app added by its Store id (the record starts without a package identity, as an MSI/EXE product),
        // whose application resource reports the identity name and the publisher but no package family name.
        const string publisher = "CN=934D5176-C6E5-455B-9AAC-3ECA3C954CB4";

        var setup = await NewSetupAsync();
        var resource = Microsoft.Apps[setup.AppId].Resource;

        resource.Remove("packageFamilyName");
        resource["packageIdentityName"] = "Milvasoft.Kabuk";
        resource["publisherName"] = publisher;

        var (_, added) = await setup.Owner.Client.PostAsync<MsProductAddedDto>("ms/product", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, productId = setup.AppId, name = "Kabuk" });

        // Act
        var addJob = await WaitForJobAsync(added.Data.JobId);
        var first = await _factory.DbAsync(db => db.MsProducts.AsNoTracking().Include(p => p.StoreApp).SingleAsync(p => p.StoreAppId == added.Data.StoreAppId));

        resource["packageFamilyName"] = "Milvasoft.Kabuk_renamed000001";

        await SyncAsync(setup);

        var second = await _factory.DbAsync(db => db.StoreApps.AsNoTracking().SingleAsync(s => s.Id == added.Data.StoreAppId));

        // Assert
        addJob.Status.Should().Be(StoreJobStatus.Succeeded, addJob.Error);
        first.StoreApp.BundleId.Should().Be(MsixIdentity.FamilyName("Milvasoft.Kabuk", publisher), "the family name is derived from the identity name and the publisher");
        first.StoreApp.ExternalId.Should().Be(setup.AppId);
        first.Kind.Should().Be(MsProductKind.Msix, "an app the MSIX API lists is an MSIX app");
        Microsoft.Calls.Should().NotContain(c => c.Contains("api.store.microsoft.com") && c.Contains(setup.AppId), "an MSIX app is not read through the MSI/EXE API");
        second.BundleId.Should().Be("Milvasoft.Kabuk_renamed000001", "every sync refreshes the package family name");
    }

    [Fact]
    public async Task MicrosoftEndpoints_ShouldHideOtherOrganizationsProducts()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var stranger = await NewSetupAsync(withApp: false);

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var own = stranger.OrganizationId;
        var other = setup.OrganizationId;

        // Act
        var responses = new List<(string Name, HttpStatusCode Status)>
        {
            ("product", (await stranger.Owner.Client.GetAsync<MsProductDto>($"ms/product?organizationId={own}&storeAppId={storeAppId}")).Http.StatusCode),
            ("submissions", (await stranger.Owner.Client.GetAsync<List<MsSubmissionDto>>($"ms/submissions?organizationId={own}&storeAppId={storeAppId}")).Http.StatusCode),
            ("flights", (await stranger.Owner.Client.GetAsync<List<MsFlightDto>>($"ms/flights?organizationId={own}&storeAppId={storeAppId}")).Http.StatusCode),
            ("addons", (await stranger.Owner.Client.GetAsync<List<MsAddonDto>>($"ms/addons?organizationId={own}&storeAppId={storeAppId}")).Http.StatusCode),
            ("packages", (await stranger.Owner.Client.GetAsync<List<MsInstallerPackageDto>>($"ms/packages?organizationId={own}&storeAppId={storeAppId}")).Http.StatusCode),
            ("content", (await stranger.Owner.Client.GetAsync<MsContentDto>($"ms/content?organizationId={own}&storeAppId={storeAppId}")).Http.StatusCode),
            ("change", (await stranger.Owner.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = own, storeAppId, change = ListingChange("x") })).Http.StatusCode),
            ("submit", (await stranger.Owner.Client.PostAsync<long>("ms/submission/submit", new { organizationId = own, storeAppId, confirm = setup.AppId })).Http.StatusCode),
            ("verify", (await stranger.Owner.Client.PostAsync<long>("ms/packages/verify", new { organizationId = own, storeAppId })).Http.StatusCode),
            ("rotate", (await stranger.Owner.Client.PutAsync<StoreConnectionDto>("ms/connection/secret", new { organizationId = own, connectionId = setup.ConnectionId, secret = Secret(setup.Tenant), credentialExpiryDate = DateTime.UtcNow.AddMonths(6) })).Http.StatusCode),
            ("product-of-other-org", (await stranger.Owner.Client.GetAsync<MsProductDto>($"ms/product?organizationId={other}&storeAppId={storeAppId}")).Http.StatusCode),
            ("change-of-other-org", (await stranger.Owner.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = other, storeAppId, change = ListingChange("x") })).Http.StatusCode),
        };

        var (content, contentBody) = await setup.Owner.Client.GetAsync<MsContentDto>($"ms/content?organizationId={other}&storeAppId={storeAppId}");

        // Assert
        responses.Should().OnlyContain(r => r.Status == HttpStatusCode.NotFound, string.Join(", ", responses.Select(r => $"{r.Name}={r.Status}")));
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        contentBody.Data.Listings.Should().ContainSingle(l => l.Description == "Old description");
        contentBody.Data.Images.Should().ContainSingle();
    }

    [Fact]
    public async Task Viewer_ShouldReadButNotChange()
    {
        // Arrange
        var setup = await NewSetupAsync();

        await SyncAsync(setup);

        var storeAppId = await StoreAppIdAsync(setup, setup.AppId);
        var viewer = await AddMemberAsync(setup, OrgRole.Viewer);

        // Act
        var (read, _) = await viewer.Client.GetAsync<MsProductDto>($"ms/product?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");
        var (change, _) = await viewer.Client.PostAsync<MsChangePlanDto>("ms/change", new { organizationId = setup.OrganizationId, storeAppId, change = ListingChange("x") });
        var (expiring, _) = await viewer.Client.GetAsync<List<MsExpiringConnectionDto>>($"ms/connections/expiring?organizationId={setup.OrganizationId}");

        // Assert
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        change.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        expiring.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<string> IconOfAsync(Setup setup, string productId)
        => _factory.DbAsync(db => db.StoreApps.Where(s => s.OrganizationId == setup.OrganizationId && s.ExternalId == productId).Select(s => s.App.IconUrl).SingleAsync());

    private static string MsLogo(string name) => $"https://store-images.s-microsoft.com/image/apps.{name}";

    [Fact]
    public async Task Sync_ShouldFillTheIcons_AndKeepThemWhenALaterReadFails()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var unpublished = NewProductId();
        var icon = MsLogo(setup.AppId);

        Microsoft.AddApp(setup.Tenant, unpublished, "Tarla", null);
        _factory.StoreGateway.Icons[setup.AppId] = icon;

        // Act
        await SyncAsync(setup);

        var first = await IconOfAsync(setup, setup.AppId);
        var firstUnpublished = await IconOfAsync(setup, unpublished);

        _factory.StoreGateway.IconFailures[setup.AppId] = new StoreCallException("503: try later", 503, true);

        await SyncAsync(setup);

        var second = await IconOfAsync(setup, setup.AppId);
        var steps = await _factory.DbAsync(db => db.StoreJobSteps.AsNoTracking()
                                                   .Where(s => s.StoreJob.OrganizationId == setup.OrganizationId && s.StoreJob.Kind == StoreJobKind.MsSync)
                                                   .OrderBy(s => s.StoreJobId).ThenBy(s => s.Order)
                                                   .Select(s => s.Name)
                                                   .ToListAsync());

        // Assert
        first.Should().Be(icon);
        firstUnpublished.Should().BeNull("the catalog has no logo for a product that is not published");
        second.Should().Be(icon, "a failed icon read neither fails the job nor clears the icon");
        steps.Take(4).Should().Equal("apps.list", "products.read", "products.save", "icons.read");
    }

    [Fact]
    public async Task Sync_ShouldNotReplaceAnAppStoreIcon_ButShouldRefreshItsOwnLogo()
    {
        // Arrange
        var setup = await NewSetupAsync();

        _factory.StoreGateway.Icons[setup.AppId] = MsLogo("old");

        await SyncAsync(setup);

        var appId = await _factory.DbAsync(db => db.StoreApps.Where(s => s.OrganizationId == setup.OrganizationId && s.ExternalId == setup.AppId).Select(s => s.AppId).SingleAsync());

        _factory.StoreGateway.Icons[setup.AppId] = MsLogo("new");

        // Act
        await SyncAsync(setup);

        var refreshed = await IconOfAsync(setup, setup.AppId);
        var appleIcon = $"https://is1-ssl.mzstatic.com/image/thumb/{setup.AppId}/512x512bb.png";

        await _factory.DbAsync(async db =>
        {
            db.StoreApps.Add(new StoreApp { OrganizationId = setup.OrganizationId, AppId = appId, Store = StoreKind.Apple, ExternalId = $"1{setup.AppId}", BundleId = $"com.{setup.AppId}", Name = "Pusula" });
            await db.SaveChangesAsync();
            await db.Apps.Where(a => a.Id == appId).ExecuteUpdateAsync(s => s.SetProperty(a => a.IconUrl, appleIcon));
        });

        _factory.StoreGateway.Icons[setup.AppId] = MsLogo("newer");

        await SyncAsync(setup);

        var kept = await IconOfAsync(setup, setup.AppId);

        // Assert
        refreshed.Should().Be(MsLogo("new"), "a logo that came from the Microsoft Store follows the store");
        kept.Should().Be(appleIcon, "an App Store or Google Play icon wins over a Microsoft Store logo");
    }
}

/// <summary>
/// A minimal signed PE file (a WIN_CERTIFICATE of revision 2.0, PKCS#7, behind the security directory).
/// </summary>
internal static class UnitTestsPe
{
    public static byte[] Signed()
    {
        var file = new byte[8192];
        const int pe = 0x80;

        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(0x3C), pe);
        "PE\0\0"u8.CopyTo(file.AsSpan(pe));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(pe + 24), 0x20B);

        var security = pe + 24 + 112 + (4 * 8);

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(security), 6000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(security + 4), 1000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(6000), 1000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(6004), 0x0200);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(6006), 0x0002);

        return file;
    }
}
