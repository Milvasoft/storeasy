using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Storeasy.Application.Dtos.EasDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "EAS builds through the Expo API: the Expo connection, project links, builds started from GitHub with the typed confirmation, following them, builds started elsewhere, cancel and the hand off to the build intake, over REST and MCP; against a fake Expo.")]
public class EasBuildsTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private FakeExpo Expo => _factory.Expo;

    private async Task<(Org Org, Record Google, string Package, string Token, string ProjectId, string Repository)> SetupAsync(bool github = true)
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.mola";
        _factory.Stores.Google.AddApp(package);
        var (_, google) = await NewAppAsync(org, null, package);
        var token = Expo.AddToken("storeasy-robot");
        var repository = github ? $"kuzey/{FakeRegistries.NewName("mola")}" : null;
        var projectId = Expo.AddProject(token, "kuzey", $"mola-{org.Suffix}", repository, package);

        var (http, saved) = await org.Owner.Client.PostAsync<ExpoConnectionDto>("eas/connection", new { organizationId = org.OrganizationId, token });

        http.StatusCode.Should().Be(HttpStatusCode.OK, saved.MessagesToString());

        return (org, google, package, token, projectId, repository);
    }

    private static async Task LinkAsync(Org org, Record app, string projectId)
    {
        var (http, linked) = await org.Owner.Client.PutAsync<EasAppDto>("eas/project", new { organizationId = org.OrganizationId, appId = app.AppId, projectId });

        http.StatusCode.Should().Be(HttpStatusCode.OK, linked.MessagesToString());
    }

    private async Task<int> FollowAsync()
    {
        using var scope = _factory.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IEasService>().FollowAsync(CancellationToken.None);
    }

    private async Task<McpClient> ConnectAsync(string token)
    {
        var http = _factory.CreateClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress, "/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        }, http, NullLoggerFactory.Instance, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static JsonElement Data(CallToolResult result) => JsonDocument.Parse(Text(result)).RootElement.GetProperty("data");

    [Fact]
    public async Task Connection_ShouldBeVerified_SealedAndShownByActorOnly()
    {
        // Arrange
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);
        var token = Expo.AddToken("kuzey-ci");

        // Act
        var (badHttp, _) = await org.Owner.Client.PostAsync<ExpoConnectionDto>("eas/connection", new { organizationId = org.OrganizationId, token = "expo_not_a_real_token_000" });
        var (http, saved) = await org.Owner.Client.PostAsync<ExpoConnectionDto>("eas/connection", new { organizationId = org.OrganizationId, token });
        var (rawHttp, _) = await org.Owner.Client.GetAsync<object>($"eas/connection?organizationId={org.OrganizationId}");
        var raw = await rawHttp.Content.ReadAsStringAsync();
        var (editorHttp, _) = await editor.Client.PostAsync<ExpoConnectionDto>("eas/connection", new { organizationId = org.OrganizationId, token });
        var (_, otherConnection) = await other.Owner.Client.GetAsync<ExpoConnectionDto>($"eas/connection?organizationId={other.OrganizationId}");
        var sealedSecret = await _factory.DbAsync(db => db.ExpoConnections.Where(c => c.OrganizationId == org.OrganizationId).Select(c => c.EncryptedSecret).SingleAsync());
        var (removeHttp, _) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "eas/connection", new { organizationId = org.OrganizationId });

        // Assert
        badHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Expo refused the token");
        http.StatusCode.Should().Be(HttpStatusCode.OK, saved.MessagesToString());
        saved.Data.Should().BeEquivalentTo(new { Fingerprint = "kuzey-ci (robot)", Status = ConnectionStatus.Verified }, o => o.ExcludingMissingMembers());
        raw.Should().NotContain(token);
        System.Text.Encoding.UTF8.GetString(sealedSecret).Should().NotContain(token);
        editorHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the token needs Manage connections");
        otherConnection.Data.Should().BeNull();
        removeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Build_ShouldStartFromGitHub_AfterTheTypedConfirmation_BeFollowed_AndHandedToTheIntake()
    {
        // Arrange
        var (org, google, package, _, projectId, repository) = await SetupAsync();
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        var appName = (await _factory.DbAsync(db => db.Apps.Where(a => a.Id == google.AppId).Select(a => a.Name).SingleAsync()));
        await org.Owner.Client.PostAsync<int>("registries/connection", new { organizationId = org.OrganizationId, kind = RegistryConnectionKind.GitHub, secret = FakeRegistryGitHub.Token });
        _factory.RegistryGitHub.SetFile(repository, "main", "eas.json", """{ "cli": { "version": ">= 12.0.0" }, "build": { "development": {}, "store-internal": { "distribution": "store" } } }""");
        await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = org.OrganizationId,
            appId = google.AppId,
            rules = new[] { new { platform = ArtifactPlatform.Android, enabled = true, target = DeliveryTarget.PlayTrack, targetId = "internal", onWarning = DeliveryWarningAction.Deliver } },
        });

        // Act
        var (_, projects) = await org.Owner.Client.GetAsync<List<EasProjectDto>>($"eas/projects?organizationId={org.OrganizationId}&appId={google.AppId}");
        await LinkAsync(org, google, projectId);
        var (_, profiles) = await org.Owner.Client.GetAsync<EasProfilesDto>($"eas/profiles?organizationId={org.OrganizationId}&appId={google.AppId}");
        var start = new { organizationId = org.OrganizationId, appId = google.AppId, platform = EasPlatform.Android, profile = "store-internal", gitRef = "release/1.4" };
        var (wrongHttp, _) = await org.Owner.Client.PostAsync<List<EasBuildDto>>("eas/build/start", new { start.organizationId, start.appId, start.platform, start.profile, start.gitRef, confirm = appName });
        var (viewerHttp, _) = await viewer.Client.PostAsync<List<EasBuildDto>>("eas/build/start", new { start.organizationId, start.appId, start.platform, start.profile, start.gitRef, confirm = $"{appName} Android" });
        var requestsBefore = Expo.GitHubBuilds.Count(r => r.StartsWith(projectId, StringComparison.Ordinal));
        var (startHttp, started) = await org.Owner.Client.PostAsync<List<EasBuildDto>>("eas/build/start", new { start.organizationId, start.appId, start.platform, start.profile, start.gitRef, confirm = $"{appName} Android" });
        Expo.ResolveReceipts();
        await FollowAsync();
        var (_, running) = await org.Owner.Client.GetAsync<EasBuildDto>($"eas/build?organizationId={org.OrganizationId}&id={started.Data[0].Id}");
        var (earlyIntake, _) = await org.Owner.Client.PostAsync<EasBuildDto>("eas/build/intake", new { organizationId = org.OrganizationId, id = running.Data.Id });
        var aab = PackageFactory.Aab(package, 140);
        var url = $"https://builds.expo.dev/artifacts/{TestApi.Unique()}/app.aab";
        _factory.Downloads.Serve(url, aab);
        Expo.SetBuild(running.Data.BuildId, "FINISHED", url);
        await FollowAsync();
        var (_, finished) = await org.Owner.Client.GetAsync<EasBuildDto>($"eas/build?organizationId={org.OrganizationId}&id={running.Data.Id}");
        var (intakeHttp, sent) = await org.Owner.Client.PostAsync<EasBuildDto>("eas/build/intake", new { organizationId = org.OrganizationId, id = running.Data.Id });
        var session = await WaitForSessionAsync(sent.Data.ArtifactSessionId!.Value);
        var (foreignHttp, _) = await (await NewOrgAsync()).Owner.Client.GetAsync<EasBuildDto>($"eas/build?organizationId={org.OrganizationId}&id={running.Data.Id}");
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());
        var notified = await _factory.DbAsync(db => db.InternalNotifications.CountAsync(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.EasBuild));

        // Assert
        projects.Data.First().Should().BeEquivalentTo(new { Id = projectId, Match = 3, GitHubRepository = repository }, o => o.ExcludingMissingMembers());
        profiles.Data.FromRepository.Should().BeTrue();
        profiles.Data.Profiles.Should().Equal("development", "store-internal");
        wrongHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        viewerHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        requestsBefore.Should().Be(0, "nothing is asked of Expo before the confirmation");
        startHttp.StatusCode.Should().Be(HttpStatusCode.OK, started.MessagesToString());
        started.Data.Should().ContainSingle().Which.Status.Should().Be(EasBuildStatus.Requested);
        Expo.GitHubBuilds.Should().Contain($"{projectId}|ANDROID|store-internal|release/1.4");
        running.Data.Status.Should().Be(EasBuildStatus.InQueue);
        running.Data.BuildId.Should().NotBeNull();
        running.Data.PageUrl.Should().Contain($"/accounts/kuzey/projects/mola-{org.Suffix}/builds/{running.Data.BuildId}");
        earlyIntake.StatusCode.Should().Be(HttpStatusCode.Conflict);
        finished.Data.Status.Should().Be(EasBuildStatus.Finished);
        finished.Data.ArtifactUrl.Should().Be(url);
        intakeHttp.StatusCode.Should().Be(HttpStatusCode.OK, sent.MessagesToString());
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);
        session.Source.Should().Be(ArtifactSource.Eas);
        foreignHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        audit.Should().Contain([AuditAction.ExpoConnectionSaved, AuditAction.EasProjectLinked, AuditAction.EasBuildStarted, AuditAction.EasBuildFinished, AuditAction.EasBuildSentToIntake]);
        notified.Should().Be(1);
    }

    [Fact]
    public async Task Start_ShouldPointToExpo_WhenTheProjectHasNoGitHubRepository_AndSyncShouldListCliBuilds()
    {
        // Arrange
        var (org, google, _, _, projectId, _) = await SetupAsync(github: false);
        var appName = await _factory.DbAsync(db => db.Apps.Where(a => a.Id == google.AppId).Select(a => a.Name).SingleAsync());
        var cliBuild = Expo.AddBuild(projectId, "IOS", "ERRORED");
        await LinkAsync(org, google, projectId);

        // Act
        var (startHttp, start) = await org.Owner.Client.PostAsync<List<EasBuildDto>>("eas/build/start", new { organizationId = org.OrganizationId, appId = google.AppId, platform = EasPlatform.All, profile = "production", gitRef = "main", confirm = $"{appName} all" });
        var (syncHttp, job) = await org.Owner.Client.PostAsync<long>("eas/sync", new { organizationId = org.OrganizationId, appId = google.AppId });
        await WaitForJobAsync(job.Data);
        var (_, app) = await org.Owner.Client.GetAsync<EasAppDto>($"eas/app?organizationId={org.OrganizationId}&appId={google.AppId}");
        var (profilesHttp, profiles) = await org.Owner.Client.GetAsync<EasProfilesDto>($"eas/profiles?organizationId={org.OrganizationId}&appId={google.AppId}");
        var (unlinkHttp, unlinked) = await org.Owner.Client.PutAsync<EasAppDto>("eas/project", new { organizationId = org.OrganizationId, appId = google.AppId, projectId = (string)null });

        // Assert
        startHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        start.MessagesToString().Should().Contain($"/accounts/kuzey/projects/mola-{org.Suffix}/github");
        Expo.GitHubBuilds.Should().NotContain(r => r.StartsWith(projectId, StringComparison.Ordinal));
        syncHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        app.Data.Link.Should().BeEquivalentTo(new { ProjectId = projectId, AccountName = "kuzey", GitHubRepository = (string)null }, o => o.ExcludingMissingMembers());
        app.Data.Builds.Should().ContainSingle(b => b.BuildId == cliBuild).Which.Should().BeEquivalentTo(new { Platform = EasPlatform.Ios, Status = EasBuildStatus.Errored, StartedInStoreasy = false, StartedBy = "cli-user" }, o => o.ExcludingMissingMembers());
        profilesHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        profiles.Data.FromRepository.Should().BeFalse();
        profiles.Data.Profiles.Should().Equal("development", "preview", "production");
        unlinkHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        unlinked.Data.Link.Should().BeNull();
    }

    [Fact]
    public async Task Mcp_ShouldListLinkStartFollowCancelAndHandOff_WithTheSameChecks()
    {
        // Arrange
        var (org, google, package, _, projectId, _) = await SetupAsync();
        var appName = await _factory.DbAsync(db => db.Apps.Where(a => a.Id == google.AppId).Select(a => a.Name).SingleAsync());
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        await using var owner = await ConnectAsync(await PatAsync(org.Owner, org.OrganizationId));
        await using var readOnly = await ConnectAsync(await PatAsync(viewer, org.OrganizationId, readOnly: true));
        var ct = TestContext.Current.CancellationToken;
        Expo.CreateAtOnce = true;

        try
        {
            // Act
            var list = await owner.CallToolAsync("eas_projects_list", new Dictionary<string, object> { ["appId"] = google.AppId }, cancellationToken: ct);
            var link = await owner.CallToolAsync("eas_project_link", new Dictionary<string, object> { ["appId"] = google.AppId, ["projectId"] = projectId }, cancellationToken: ct);
            var args = new Dictionary<string, object> { ["appId"] = google.AppId, ["platform"] = "all", ["profile"] = "preview", ["gitRef"] = "main", ["confirmText"] = $"{appName} all" };
            var withoutConfirm = await owner.CallToolAsync("eas_build_start", args, cancellationToken: ct);
            var asViewer = await readOnly.CallToolAsync("eas_build_start", new Dictionary<string, object>(args) { ["confirm"] = true }, cancellationToken: ct);
            var started = await owner.CallToolAsync("eas_build_start", new Dictionary<string, object>(args) { ["confirm"] = true }, cancellationToken: ct);
            var ids = Data(started).EnumerateArray().Select(b => b.GetProperty("id").GetInt64()).ToList();
            var get = await owner.CallToolAsync("eas_build_get", new Dictionary<string, object> { ["id"] = ids[0] }, cancellationToken: ct);
            var cancel = await owner.CallToolAsync("eas_build_cancel", new Dictionary<string, object> { ["id"] = ids[0] }, cancellationToken: ct);
            var buildId = Data(get).GetProperty("buildId").GetString();
            var android = await _factory.DbAsync(db => db.EasBuilds.Where(b => b.Id == ids[1]).Select(b => b.BuildId).SingleAsync());
            var aab = PackageFactory.Aab(package, 141);
            var url = $"https://builds.expo.dev/artifacts/{TestApi.Unique()}/app.aab";
            _factory.Downloads.Serve(url, aab);
            Expo.SetBuild(android, "FINISHED", url);
            await FollowAsync();
            var handOff = await owner.CallToolAsync("eas_build_send_to_intake", new Dictionary<string, object> { ["id"] = ids[1] }, cancellationToken: ct);
            var builds = await readOnly.CallToolAsync("eas_builds_list", new Dictionary<string, object> { ["appId"] = google.AppId }, cancellationToken: ct);
            var channels = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == org.OrganizationId && (a.Action == AuditAction.EasBuildStarted || a.Action == AuditAction.EasBuildCanceled || a.Action == AuditAction.EasBuildSentToIntake)).Select(a => a.Channel).ToListAsync());

            // Assert
            list.IsError.Should().NotBe(true, Text(list));
            Data(list)[0].GetProperty("id").GetString().Should().Be(projectId);
            link.IsError.Should().NotBe(true, Text(link));
            withoutConfirm.IsError.Should().BeTrue();
            Text(withoutConfirm).Should().Contain(MessageKey.McpConfirmRequired);
            asViewer.IsError.Should().BeTrue();
            Text(asViewer).Should().Contain(MessageKey.OrganizationForbidden);
            started.IsError.Should().NotBe(true, Text(started));
            ids.Should().HaveCount(2);
            get.IsError.Should().NotBe(true, Text(get));
            buildId.Should().NotBeNull("Expo created the builds at once");
            cancel.IsError.Should().NotBe(true, Text(cancel));
            Expo.Cancels.Should().Contain(buildId);
            handOff.IsError.Should().NotBe(true, Text(handOff));
            Data(handOff).GetProperty("artifactSessionId").ValueKind.Should().Be(JsonValueKind.Number);
            builds.IsError.Should().NotBe(true, Text(builds));
            Data(builds).GetProperty("builds").GetArrayLength().Should().Be(2);
            channels.Count(c => c == ActionChannel.Mcp).Should().Be(4, "the starts, the cancel and the hand off came over MCP");
        }
        finally
        {
            Expo.CreateAtOnce = false;
        }
    }

    private async Task<(Org Org, Record Google, string Package, string ProjectId, long BuildRowId, string BuildId)> FinishedAndroidBuildAsync(object chain, int versionCode)
    {
        var (org, google, package, _, projectId, _) = await SetupAsync();
        var appName = await _factory.DbAsync(db => db.Apps.Where(a => a.Id == google.AppId).Select(a => a.Name).SingleAsync());
        await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = org.OrganizationId,
            appId = google.AppId,
            rules = new[] { new { platform = ArtifactPlatform.Android, enabled = true, target = DeliveryTarget.PlayTrack, targetId = "internal", onWarning = DeliveryWarningAction.Deliver } },
        });
        await LinkAsync(org, google, projectId);
        var (http, started) = await org.Owner.Client.PostAsync<List<EasBuildDto>>("eas/build/start", new { organizationId = org.OrganizationId, appId = google.AppId, platform = EasPlatform.Android, profile = "production", gitRef = "main", chain, confirm = $"{appName} Android" });

        http.StatusCode.Should().Be(HttpStatusCode.OK, started.MessagesToString());
        Expo.ResolveReceipts();
        await FollowAsync();

        var buildId = await _factory.DbAsync(db => db.EasBuilds.Where(b => b.Id == started.Data[0].Id).Select(b => b.BuildId).SingleAsync());
        var url = $"https://builds.expo.dev/artifacts/{TestApi.Unique()}/app.aab";

        _factory.Downloads.Serve(url, PackageFactory.Aab(package, versionCode));
        Expo.Builds[buildId]["appBuildVersion"] = versionCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Expo.SetBuild(buildId, "FINISHED", url);

        return (org, google, package, projectId, started.Data[0].Id, buildId);
    }

    [Fact]
    public async Task Chain_ShouldSendToIntake_ThenPrepareTheProductionPlan_WithoutApprovingIt()
    {
        // Arrange
        var (org, _, _, _, buildRowId, _) = await FinishedAndroidBuildAsync(new { afterBuild = EasAfterBuild.Intake, prepareReviewPlan = true }, 150);

        // Act
        await FollowAsync();
        var sessionId = await _factory.DbAsync(db => db.EasBuilds.Where(b => b.Id == buildRowId).Select(b => b.ArtifactSessionId).SingleAsync());
        var session = await WaitForSessionAsync(sessionId!.Value);
        await FollowAsync();
        var build = await _factory.DbAsync(db => db.EasBuilds.AsNoTracking().SingleAsync(b => b.Id == buildRowId));
        var plans = await _factory.DbAsync(db => db.ChangePlans.AsNoTracking().Where(p => build.ReviewPlanIds.Contains(p.Id)).ToListAsync());
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());

        // Assert
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);
        build.ChainDone.Should().BeTrue();
        build.ChainError.Should().BeNull();
        build.ReviewPlanDone.Should().BeTrue();
        plans.Should().ContainSingle();
        plans[0].Status.Should().Be(ChangePlanStatus.Pending, "a prepared plan is never approved");
        plans[0].RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        plans[0].ConfirmText.Should().NotBeNullOrEmpty();
        audit.Should().Contain([AuditAction.EasBuildSentToIntake, AuditAction.EasReviewPlanPrepared]);
    }

    [Fact]
    public async Task Submit_ShouldRunAsTheChainOption_AndOnRequest_WithTheSameChecks()
    {
        // Arrange
        var (org, google, package, projectId, buildRowId, buildId) = await FinishedAndroidBuildAsync(new { afterBuild = EasAfterBuild.Submit, submitTrack = "internal", submitReleaseStatus = "DRAFT" }, 151);
        var editor = await MemberAsync(org, OrgRole.Editor);
        var appName = await _factory.DbAsync(db => db.Apps.Where(a => a.Id == google.AppId).Select(a => a.Name).SingleAsync());

        // Act
        await FollowAsync();
        var missing = await _factory.DbAsync(db => db.EasBuilds.Where(b => b.Id == buildRowId).Select(b => b.ChainError).SingleAsync());
        Expo.SubmitCredentials[projectId] = true;
        var (editorHttp, _) = await editor.Client.PostAsync<EasSubmissionDto>("eas/submission/start", new { organizationId = org.OrganizationId, id = buildRowId, track = "production", releaseStatus = "COMPLETED", confirm = $"{appName} Android" });
        var (wrongHttp, _) = await org.Owner.Client.PostAsync<EasSubmissionDto>("eas/submission/start", new { organizationId = org.OrganizationId, id = buildRowId, track = "internal", releaseStatus = "DRAFT", confirm = appName });
        var (http, submission) = await org.Owner.Client.PostAsync<EasSubmissionDto>("eas/submission/start", new { organizationId = org.OrganizationId, id = buildRowId, track = "internal", releaseStatus = "DRAFT", confirm = $"{appName} Android" });
        Expo.SetSubmission(submission.Data.SubmissionId, "FINISHED");
        await FollowAsync();
        var (_, finished) = await org.Owner.Client.GetAsync<EasSubmissionDto>($"eas/submission?organizationId={org.OrganizationId}&id={submission.Data.Id}");
        var (_, list) = await org.Owner.Client.GetAsync<List<EasSubmissionDto>>($"eas/submissions?organizationId={org.OrganizationId}&appId={google.AppId}");
        var (foreignHttp, _) = await (await NewOrgAsync()).Owner.Client.GetAsync<EasSubmissionDto>($"eas/submission?organizationId={org.OrganizationId}&id={submission.Data.Id}");
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());

        // Assert
        missing.Should().Contain("/credentials", "the project has no submit credentials on expo.dev yet");
        editorHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the production track needs the production release permission");
        wrongHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        http.StatusCode.Should().Be(HttpStatusCode.OK, submission.MessagesToString());
        Expo.SubmissionRequests.Should().Contain(r => r.StartsWith($"ANDROID|{buildId}|", StringComparison.Ordinal) && r.Contains("\"track\":\"internal\"", StringComparison.Ordinal) && r.Contains("gsa-key-1", StringComparison.Ordinal) && r.Contains(package, StringComparison.Ordinal));
        finished.Data.Status.Should().Be(EasSubmissionStatus.Finished);
        list.Data.Should().ContainSingle(s => s.Id == submission.Data.Id);
        foreignHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        audit.Should().Contain([AuditAction.EasSubmissionStarted, AuditAction.EasSubmissionFinished]);
    }

    [Fact]
    public async Task Mcp_ShouldSaveDefaults_SubmitAndFollowSubmissions()
    {
        // Arrange
        var (org, google, _, projectId, buildRowId, _) = await FinishedAndroidBuildAsync(new { afterBuild = EasAfterBuild.None }, 152);
        await FollowAsync();
        Expo.SubmitCredentials[projectId] = true;
        var appName = await _factory.DbAsync(db => db.Apps.Where(a => a.Id == google.AppId).Select(a => a.Name).SingleAsync());
        await using var owner = await ConnectAsync(await PatAsync(org.Owner, org.OrganizationId));
        var ct = TestContext.Current.CancellationToken;

        // Act
        var defaults = await owner.CallToolAsync("eas_chain_save", new Dictionary<string, object> { ["appId"] = google.AppId, ["chain"] = new { afterBuild = "Submit", submitTrack = "beta", submitReleaseStatus = "COMPLETED", prepareReviewPlan = true } }, cancellationToken: ct);
        var withoutConfirm = await owner.CallToolAsync("eas_submit_start", new Dictionary<string, object> { ["id"] = buildRowId, ["track"] = "internal", ["confirmText"] = $"{appName} Android" }, cancellationToken: ct);
        var started = await owner.CallToolAsync("eas_submit_start", new Dictionary<string, object> { ["id"] = buildRowId, ["track"] = "internal", ["releaseStatus"] = "COMPLETED", ["confirm"] = true, ["confirmText"] = $"{appName} Android" }, cancellationToken: ct);
        var id = Data(started).GetProperty("id").GetInt64();
        Expo.SetSubmission(Data(started).GetProperty("submissionId").GetString(), "ERRORED", "The bundle was rejected.");
        await FollowAsync();
        var get = await owner.CallToolAsync("eas_submission_get", new Dictionary<string, object> { ["id"] = id }, cancellationToken: ct);
        var list = await owner.CallToolAsync("eas_submissions_list", new Dictionary<string, object> { ["appId"] = google.AppId }, cancellationToken: ct);
        var link = await _factory.DbAsync(db => db.EasProjectLinks.AsNoTracking().SingleAsync(l => l.AppId == google.AppId));

        // Assert
        defaults.IsError.Should().NotBe(true, Text(defaults));
        link.AfterBuild.Should().Be(EasAfterBuild.Submit);
        link.SubmitTrack.Should().Be("beta");
        link.PrepareReviewPlan.Should().BeTrue();
        withoutConfirm.IsError.Should().BeTrue();
        Text(withoutConfirm).Should().Contain(MessageKey.McpConfirmRequired);
        started.IsError.Should().NotBe(true, Text(started));
        Data(get).GetProperty("status").GetString().Should().Be("errored");
        Data(get).GetProperty("errorMessage").GetString().Should().Be("The bundle was rejected.");
        Data(list).GetArrayLength().Should().Be(1);
    }
}
