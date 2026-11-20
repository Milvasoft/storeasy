using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Store job queue, runner, retries, cancellation and the job endpoints.")]
public class StoreJobsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private async Task<(TestUser Owner, int OrganizationId)> NewOrganizationAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (http, body) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        return (owner, body.Data);
    }

    private Task<long> EnqueueAsync(int organizationId, string mode, StoreJobKind kind = FakeStoreJobHandler.TestKind, int? maxAttempts = null)
        => _factory.WithServiceAsync<IStoreJobQueue, long>(async queue => (await queue.EnqueueAsync([new StoreJobRequest(organizationId, kind, new FakeStoreJobHandler.Script(mode), StoreKind.Apple, MaxAttempts: maxAttempts)], CancellationToken.None))[0]);

    private async Task<StoreJob> WaitForAsync(long jobId, Func<StoreJob, bool> condition)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

            if (condition(job))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status} (attempt {job.Attempt}).");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private Task<StoreJob> WaitForEndAsync(long jobId) => WaitForAsync(jobId, j => StoreJobRules.IsFinished(j.Status));

    private async Task<long> InsertJobAsync(int organizationId, StoreJobStatus status, DateTime nextAttemptDate, DateTime? lockedUntil = null, int attempt = 0)
    {
        var job = new StoreJob
        {
            OrganizationId = organizationId,
            Kind = FakeStoreJobHandler.TestKind,
            Payload = "{\"Mode\":\"succeed\"}",
            Status = status,
            Attempt = attempt,
            MaxAttempts = 3,
            NextAttemptDate = nextAttemptDate,
            LockedUntil = lockedUntil,
            RequestedByUserName = "test",
            CreationDate = DateTime.UtcNow,
        };

        await _factory.DbAsync(async db =>
        {
            db.StoreJobs.Add(job);
            await db.SaveChangesAsync();
        });

        return job.Id;
    }

    [Fact]
    public async Task QueuedJob_ShouldRunToSuccessWithItsSteps()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();

        // Act
        var jobId = await EnqueueAsync(organizationId, FakeStoreJobHandler.Succeed);
        var job = await WaitForEndAsync(jobId);

        var (detailHttp, detail) = await owner.Client.GetAsync<StoreJobDetailDto>($"storejobs/job?organizationId={organizationId}&jobId={jobId}");
        var (_, list) = await owner.Client.PatchListAsync<StoreJobListDto>("storejobs", new { organizationId, pageNumber = 1, rowCount = 10 });

        // Assert
        job.Status.Should().Be(StoreJobStatus.Succeeded);
        job.Attempt.Should().Be(1);
        job.LockedUntil.Should().BeNull();
        detailHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Data.Summary.Should().Be("Done on attempt 1.");
        detail.Data.Steps.Select(s => (s.Name, s.Status)).Should().Equal(("test.call", StoreJobStepStatus.Succeeded), ("test.save", StoreJobStepStatus.Succeeded));
        detail.Data.Steps[0].Progress.Should().Be(10);
        list.Data.Should().ContainSingle(j => j.Id == jobId).Which.Status.Should().Be(StoreJobStatus.Succeeded);
    }

    [Fact]
    public async Task TransientFailure_ShouldBeRetriedAndSucceed()
    {
        var (_, organizationId) = await NewOrganizationAsync();

        var job = await WaitForEndAsync(await EnqueueAsync(organizationId, FakeStoreJobHandler.TransientOnce));

        job.Status.Should().Be(StoreJobStatus.Succeeded);
        job.Attempt.Should().Be(2);
        job.Error.Should().BeNull();

        var steps = await _factory.DbAsync(db => db.StoreJobSteps.AsNoTracking().Where(s => s.StoreJobId == job.Id).OrderBy(s => s.Order).ToListAsync());

        steps.First().Should().Match<StoreJobStep>(s => s.Attempt == 1 && s.Status == StoreJobStepStatus.Failed);
        steps.Select(s => s.Order).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task TransientFailureOnEveryAttempt_ShouldFailAfterTheLastAttempt()
    {
        var (_, organizationId) = await NewOrganizationAsync();

        var job = await WaitForEndAsync(await EnqueueAsync(organizationId, FakeStoreJobHandler.TransientAlways, maxAttempts: 2));

        job.Status.Should().Be(StoreJobStatus.Failed);
        job.Attempt.Should().Be(2);
        job.Error.Should().Contain("503");
    }

    [Fact]
    public async Task PermanentFailure_ShouldNotBeRetried()
    {
        var (_, organizationId) = await NewOrganizationAsync();

        var job = await WaitForEndAsync(await EnqueueAsync(organizationId, FakeStoreJobHandler.Fail));

        job.Status.Should().Be(StoreJobStatus.Failed);
        job.Attempt.Should().Be(1);
        job.Error.Should().Be("The store refused the request.");
    }

    [Fact]
    public async Task JobWithoutAHandler_ShouldFail()
    {
        var (_, organizationId) = await NewOrganizationAsync();

        var job = await WaitForEndAsync(await EnqueueAsync(organizationId, FakeStoreJobHandler.Succeed, FakeStoreJobHandler.UnknownKind));

        job.Status.Should().Be(StoreJobStatus.Failed);
        job.Error.Should().Contain("No handler");
    }

    [Fact]
    public async Task RunningJobWithAnExpiredLease_ShouldBeClaimedAgain()
    {
        var (_, organizationId) = await NewOrganizationAsync();

        var jobId = await InsertJobAsync(organizationId, StoreJobStatus.Running, DateTime.UtcNow.AddMinutes(-10), lockedUntil: DateTime.UtcNow.AddMinutes(-1), attempt: 1);
        var job = await WaitForEndAsync(jobId);

        job.Status.Should().Be(StoreJobStatus.Succeeded);
        job.Attempt.Should().Be(2);
    }

    [Fact]
    public async Task CancelQueuedJob_ShouldCancelItAtOnce()
    {
        // Arrange: not due for an hour, so the runner leaves it alone.
        var (owner, organizationId) = await NewOrganizationAsync();
        var jobId = await InsertJobAsync(organizationId, StoreJobStatus.Queued, DateTime.UtcNow.AddHours(1));

        // Act
        var (http, body) = await owner.Client.PostAsync<StoreJobStatus>("storejobs/job/cancel", new { organizationId, jobId });
        var (again, _) = await owner.Client.PostAsync<StoreJobStatus>("storejobs/job/cancel", new { organizationId, jobId });
        var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));
        var audited = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().AnyAsync(a => a.OrganizationId == organizationId && a.Action == AuditAction.JobCancelled && a.TargetId == jobId.ToString()));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Should().Be(StoreJobStatus.Cancelled);
        job.Status.Should().Be(StoreJobStatus.Cancelled);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task CancelRunningJob_ShouldStopItAtTheNextStep()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var jobId = await EnqueueAsync(organizationId, FakeStoreJobHandler.Wait);

        await WaitForAsync(jobId, j => j.Status == StoreJobStatus.Running);

        // Act
        var (http, body) = await owner.Client.PostAsync<StoreJobStatus>("storejobs/job/cancel", new { organizationId, jobId });
        var job = await WaitForEndAsync(jobId);
        var openSteps = await _factory.DbAsync(db => db.StoreJobSteps.AsNoTracking().CountAsync(s => s.StoreJobId == jobId && s.Status == StoreJobStepStatus.Running));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Should().Be(StoreJobStatus.Running);
        job.Status.Should().Be(StoreJobStatus.Cancelled);
        openSteps.Should().Be(0);
    }

    [Fact]
    public async Task JobEndpoints_ShouldHideOtherOrganizationsAndCheckRoles()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var (stranger, otherOrganizationId) = await NewOrganizationAsync();
        var viewerUser = await _factory.CreateUserAsync(email: $"v{TestApi.Unique()}@example.com");
        var viewer = await _factory.LoginAsync(viewerUser.UserName, TestApi.DefaultPassword);

        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = viewerUser.Email, role = OrgRole.Viewer });
        await viewer.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        var jobId = await InsertJobAsync(organizationId, StoreJobStatus.Queued, DateTime.UtcNow.AddHours(1));

        // Act
        var (strangerDetail, _) = await stranger.Client.GetAsync<StoreJobDetailDto>($"storejobs/job?organizationId={organizationId}&jobId={jobId}");
        var (strangerList, _) = await stranger.Client.PatchListAsync<StoreJobListDto>("storejobs", new { organizationId, pageNumber = 1, rowCount = 10 });
        var (wrongOrganization, _) = await stranger.Client.GetAsync<StoreJobDetailDto>($"storejobs/job?organizationId={otherOrganizationId}&jobId={jobId}");
        var (viewerDetail, _) = await viewer.Client.GetAsync<StoreJobDetailDto>($"storejobs/job?organizationId={organizationId}&jobId={jobId}");
        var (viewerCancel, _) = await viewer.Client.PostAsync<StoreJobStatus>("storejobs/job/cancel", new { organizationId, jobId });

        // Assert
        strangerDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        strangerList.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongOrganization.StatusCode.Should().Be(HttpStatusCode.NotFound, "a job of another organization is not found");
        viewerDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        viewerCancel.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Board_ShouldNameReviewsBuildsAndTheLast24HoursOfTheOrganization()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var stranger = await _factory.NewOrgAsync();
        var (appId, storeAppId, externalId) = await _factory.AddAppAsync(org, name: "Pusula");
        var (otherAppId, otherStoreAppId, _) = await _factory.AddAppAsync(org, name: "Mola");
        var (msAppId, msStoreAppId, msProductId) = await _factory.AddAppAsync(org, StoreKind.Microsoft, name: "Kabuk");
        var (manualAppId, manualStoreAppId, _) = await _factory.AddAppAsync(org, StoreKind.Microsoft, name: "Kiler");
        var (_, immediateStoreAppId, _) = await _factory.AddAppAsync(org, StoreKind.Microsoft, name: "Kuyu");
        var now = DateTime.UtcNow;

        await _factory.DbAsync(async db =>
        {
            db.AppVersionStates.AddRange(
                new AppVersionState { OrganizationId = org.OrganizationId, AppId = appId, StoreAppId = storeAppId, Store = StoreKind.Apple, ExternalVersionId = $"v1{org.Suffix}", VersionString = "2.3.0", State = "READY_FOR_SALE", ChangedDate = now.AddDays(-60), SubmittedDate = now.AddDays(-62) },
                new AppVersionState { OrganizationId = org.OrganizationId, AppId = appId, StoreAppId = storeAppId, Store = StoreKind.Apple, ExternalVersionId = $"v2{org.Suffix}", VersionString = "2.3.1", BuildNumber = "400", State = "IN_REVIEW", PreviousState = "WAITING_FOR_REVIEW", ChangedDate = now.AddHours(-26), SubmittedDate = now.AddHours(-40) },
                new AppVersionState { OrganizationId = org.OrganizationId, AppId = otherAppId, StoreAppId = otherStoreAppId, Store = StoreKind.Apple, ExternalVersionId = $"v3{org.Suffix}", VersionString = "1.0", State = "READY_FOR_SALE", ChangedDate = now.AddDays(-45) });

            db.StoreJobs.AddRange(
                new StoreJob { OrganizationId = org.OrganizationId, Kind = FakeStoreJobHandler.TestKind, Store = StoreKind.Apple, Status = StoreJobStatus.Succeeded, Payload = "{}", MaxAttempts = 1, NextAttemptDate = now, RequestedByUserName = "test", CreationDate = now, EndDate = now.AddMinutes(-5) },
                new StoreJob { OrganizationId = org.OrganizationId, Kind = FakeStoreJobHandler.TestKind, Store = StoreKind.Google, Status = StoreJobStatus.Failed, Payload = "{}", MaxAttempts = 1, NextAttemptDate = now, RequestedByUserName = "test", CreationDate = now, EndDate = now.AddMinutes(-5) },
                new StoreJob { OrganizationId = org.OrganizationId, Kind = FakeStoreJobHandler.TestKind, Store = StoreKind.Apple, Status = StoreJobStatus.Succeeded, Payload = "{}", MaxAttempts = 1, NextAttemptDate = now, RequestedByUserName = "test", CreationDate = now, EndDate = now.AddDays(-3) });

            // A certified Microsoft Store submission waiting for "Publish now", and one still in certification.
            db.MsSubmissions.AddRange(
                new MsSubmission { OrganizationId = org.OrganizationId, StoreAppId = msStoreAppId, Target = MsSubmissionTarget.App, ExternalId = $"5{org.Suffix}", PackageVersion = "1.0.15.0", Status = MsSubmissionStatus.PendingPublication, IsPending = true, CreationDate = now, StatusHistory = [new MsStatusChange { Status = MsSubmissionStatus.Certification, Date = now.AddHours(-30) }, new MsStatusChange { Status = MsSubmissionStatus.PendingPublication, Date = now.AddHours(-2) }] },
                new MsSubmission { OrganizationId = org.OrganizationId, StoreAppId = msStoreAppId, Target = MsSubmissionTarget.Flight, ExternalId = $"6{org.Suffix}", FlightId = "f1", PackageVersion = "1.0.16.0", Status = MsSubmissionStatus.Certification, IsPending = true, CreationDate = now },
                // A Manual submission the API reports as Release while Partner Center waits for "Publish now", and an
                // Immediate one in Release (really being released).
                new MsSubmission { OrganizationId = org.OrganizationId, StoreAppId = manualStoreAppId, Target = MsSubmissionTarget.App, ExternalId = $"7{org.Suffix}", PackageVersion = "3.1.0.0", Status = MsSubmissionStatus.Release, PublishMode = MsPublishMode.Manual, IsPending = true, CreationDate = now, StatusHistory = [new MsStatusChange { Status = MsSubmissionStatus.Certification, Date = now.AddHours(-20) }, new MsStatusChange { Status = MsSubmissionStatus.Release, Date = now.AddHours(-4) }] },
                new MsSubmission { OrganizationId = org.OrganizationId, StoreAppId = immediateStoreAppId, Target = MsSubmissionTarget.App, ExternalId = $"8{org.Suffix}", PackageVersion = "4.0.0.0", Status = MsSubmissionStatus.Release, PublishMode = MsPublishMode.Immediate, IsPending = true, CreationDate = now, StatusHistory = [new MsStatusChange { Status = MsSubmissionStatus.Release, Date = now.AddHours(-1) }] });

            await db.SaveChangesAsync();
        });

        await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b1{externalId}", "2.3.2", "412", null, BuildProcessingState.Processing);
        await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b2{externalId}", "2.2.4", "398", now.AddDays(3));
        await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b3{externalId}", "2.2.0", "390", now.AddDays(40));

        // Act
        var (http, body) = await org.Owner.Client.GetAsync<ActivityBoardDto>($"storejobs/board?organizationId={org.OrganizationId}");
        var (strangerHttp, _) = await stranger.Owner.Client.GetAsync<ActivityBoardDto>($"storejobs/board?organizationId={org.OrganizationId}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        strangerHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Data.Versions.Where(v => v.Store == StoreKind.Apple).Should().ContainSingle("the latest state of each record, and only recent or in review ones")
            .Which.Should().Match<ActivityBoardVersionDto>(v => v.AppId == appId && v.State == "IN_REVIEW" && v.VersionString == "2.3.1" && v.SubmittedDate != null && v.ExternalId == externalId);
        body.Data.Versions.Where(v => v.Store == StoreKind.Microsoft).Should().HaveCount(2, "only the submissions waiting for Publish now");
        body.Data.Versions.Should().ContainSingle(v => v.StoreAppId == msStoreAppId)
            .Which.Should().Match<ActivityBoardVersionDto>(v => v.AppId == msAppId && v.State == "PENDINGPUBLICATION" && v.VersionString == "1.0.15.0" && v.ExternalId == msProductId);
        body.Data.Versions.Should().ContainSingle(v => v.StoreAppId == manualStoreAppId, "a Manual submission in Release waits for Publish now too")
            .Which.Should().Match<ActivityBoardVersionDto>(v => v.AppId == manualAppId && v.State == "PENDINGPUBLICATION" && v.VersionString == "3.1.0.0");
        body.Data.Versions.Single(v => v.StoreAppId == manualStoreAppId).ChangedDate.Should().BeCloseTo(now.AddHours(-4), TimeSpan.FromSeconds(1), "the Release change is when it started waiting");
        body.Data.Versions.Should().NotContain(v => v.StoreAppId == immediateStoreAppId, "an Immediate submission in Release is being released");
        body.Data.Versions[0].StoreAppId.Should().Be(msStoreAppId, "the newest change comes first");
        body.Data.Builds.Select(b => (b.BuildNumber, b.ProcessingState, b.AppId)).Should().Equal(("412", BuildProcessingState.Processing, appId), ("398", BuildProcessingState.Valid, appId));
        body.Data.Hours.Should().HaveCount(2);
        body.Data.Hours.Should().ContainSingle(h => h.Store == StoreKind.Apple).Which.Succeeded.Should().Be(1);
        body.Data.Hours.Should().ContainSingle(h => h.Store == StoreKind.Google).Which.Failed.Should().Be(1);
        body.Data.Hours.Should().OnlyContain(h => h.Hour >= body.Data.HoursStartDate && h.Hour <= body.Data.GeneratedDate);
    }

    [Fact]
    public async Task Board_ShouldLeaveMutedAppsOutAndNameThem()
    {
        // Arrange: two apps rejected by App Review, each with a processing build and a build close to expiry, and a
        // Microsoft Store product waiting for "Publish now"; the first app and the product are muted.
        var org = await _factory.NewOrgAsync();
        var (mutedAppId, mutedStoreAppId, mutedExternalId) = await _factory.AddAppAsync(org, name: "Expenny");
        var (loudAppId, loudStoreAppId, loudExternalId) = await _factory.AddAppAsync(org, name: "Pusula");
        var (mutedMsAppId, mutedMsStoreAppId, _) = await _factory.AddAppAsync(org, StoreKind.Microsoft, name: "Kabuk");
        var now = DateTime.UtcNow;

        await _factory.DbAsync(async db =>
        {
            db.AppVersionStates.AddRange(
                new AppVersionState { OrganizationId = org.OrganizationId, AppId = mutedAppId, StoreAppId = mutedStoreAppId, Store = StoreKind.Apple, ExternalVersionId = $"m1{org.Suffix}", VersionString = "1.2", State = "REJECTED", PreviousState = "IN_REVIEW", ChangedDate = now.AddHours(-1) },
                new AppVersionState { OrganizationId = org.OrganizationId, AppId = loudAppId, StoreAppId = loudStoreAppId, Store = StoreKind.Apple, ExternalVersionId = $"l1{org.Suffix}", VersionString = "3.0", State = "REJECTED", PreviousState = "IN_REVIEW", ChangedDate = now.AddHours(-2) });

            db.MsSubmissions.Add(new MsSubmission { OrganizationId = org.OrganizationId, StoreAppId = mutedMsStoreAppId, Target = MsSubmissionTarget.App, ExternalId = $"9{org.Suffix}", PackageVersion = "1.0.1.0", Status = MsSubmissionStatus.PendingPublication, IsPending = true, CreationDate = now });

            await db.SaveChangesAsync();
        });

        await _factory.AddBuildAsync(org.OrganizationId, mutedStoreAppId, $"b1{mutedExternalId}", "1.3", "20", null, BuildProcessingState.Processing);
        await _factory.AddBuildAsync(org.OrganizationId, mutedStoreAppId, $"b2{mutedExternalId}", "1.2", "19", now.AddDays(2));
        await _factory.AddBuildAsync(org.OrganizationId, loudStoreAppId, $"b1{loudExternalId}", "3.1", "70", null, BuildProcessingState.Processing);
        await _factory.AddBuildAsync(org.OrganizationId, loudStoreAppId, $"b2{loudExternalId}", "3.0", "69", now.AddDays(2));

        foreach (var appId in new[] { mutedAppId, mutedMsAppId })
        {
            var (mute, _) = await org.Owner.Client.PostAsync<int>("apps/app/alerts-muted", new { organizationId = org.OrganizationId, appId, muted = true });

            mute.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Act
        var (http, body) = await org.Owner.Client.GetAsync<ActivityBoardDto>($"storejobs/board?organizationId={org.OrganizationId}");
        await org.Owner.Client.PostAsync<int>("apps/app/alerts-muted", new { organizationId = org.OrganizationId, appId = mutedAppId, muted = false });
        var (_, afterUnmute) = await org.Owner.Client.GetAsync<ActivityBoardDto>($"storejobs/board?organizationId={org.OrganizationId}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Versions.Should().ContainSingle("the muted apps' rejection and waiting submission are left out")
            .Which.Should().Match<ActivityBoardVersionDto>(v => v.AppId == loudAppId && v.State == "REJECTED");
        body.Data.Builds.Should().HaveCount(2).And.OnlyContain(b => b.AppId == loudAppId, "the muted app's builds are left out");
        body.Data.MutedAppIds.Should().BeEquivalentTo([mutedAppId, mutedMsAppId]);
        afterUnmute.Data.Versions.Select(v => v.AppId).Should().BeEquivalentTo([mutedAppId, loudAppId], "the records were kept and come back when the alerts are on again");
        afterUnmute.Data.Builds.Should().HaveCount(4);
        afterUnmute.Data.MutedAppIds.Should().Equal(mutedMsAppId);
    }
}
