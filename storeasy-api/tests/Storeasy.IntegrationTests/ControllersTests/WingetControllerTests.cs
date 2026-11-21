using FluentAssertions;
using Milvasoft.Components.Rest.MilvaResponse;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "winget: connection, package sync, manifest plans, pull request and review state; against a fake GitHub.")]
public class WingetControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private FakeGitHub GitHub => _factory.GitHub;

    private sealed record Setup(TestUser Owner, int OrganizationId, int ConnectionId, string Token, string Login, string Fork, string PackageId);

    private static string Secret(string token, string fork) => JsonSerializer.Serialize(new { token, fork });

    private static string NewPackageId() => $"Milvasoft.Kabuk{TestApi.Unique()}";

    private static Dictionary<string, string> Manifests(string id, string version) => new()
    {
        [$"{id}.yaml"] = $"PackageIdentifier: {id}\nPackageVersion: {version}\nDefaultLocale: en-US\nManifestType: version\nManifestVersion: 1.12.0\n",
        [$"{id}.installer.yaml"] = $"""
            PackageIdentifier: {id}
            PackageVersion: {version}
            InstallerType: exe
            InstallerSwitches:
              Silent: /S
              SilentWithProgress: /S
            Installers:
            - Architecture: x64
              Scope: user
              InstallerUrl: https://download.milvasoft.com/kabuk/{version}/KabukSetup.exe
              InstallerSha256: {new string('1', 64)}
            ManifestType: installer
            ManifestVersion: 1.12.0

            """.ReplaceLineEndings("\n"),
        [$"{id}.locale.en-US.yaml"] = $"""
            PackageIdentifier: {id}
            PackageVersion: {version}
            PackageLocale: en-US
            Publisher: Milvasoft
            PublisherUrl: https://milvasoft.com
            PackageName: Kabuk
            License: Proprietary
            ShortDescription: A fast terminal.
            ReleaseNotes: Notes of {version}
            ManifestType: defaultLocale
            ManifestVersion: 1.12.0

            """.ReplaceLineEndings("\n"),
    };

    private async Task<Setup> NewSetupAsync(bool withPackage = true)
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var (token, login, fork) = GitHub.AddAccount();
        var (http, connection) = await owner.Client.PostAsync<int>("storeconnections/connection", new
        {
            organizationId = organization.Data,
            store = StoreKind.Winget,
            name = "winget fork",
            secret = Secret(token, fork),
            icon = "winget",
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", connection.Messages?.Select(m => m.Message) ?? []));

        var packageId = NewPackageId();

        if (withPackage)
            GitHub.AddVersion(packageId, "1.2.0", Manifests(packageId, "1.2.0"));

        return new Setup(owner, organization.Data, connection.Data, token, login, fork, packageId);
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

    private async Task<WingetPackageAddedDto> AddPackageAsync(Setup setup)
    {
        var (http, added) = await setup.Owner.Client.PostAsync<WingetPackageAddedDto>("winget/package", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, packageIdentifier = setup.PackageId });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", added.Messages?.Select(m => m.Message) ?? []));

        var job = await WaitForJobAsync(added.Data.JobId);

        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);

        return added.Data;
    }

    private async Task<WingetPackageDto> GetPackageAsync(Setup setup, int storeAppId)
    {
        var (http, package) = await setup.Owner.Client.GetAsync<WingetPackageDto>($"winget/package?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        return package.Data;
    }

    private async Task SyncAsync(Setup setup, int storeAppId)
    {
        var (http, job) = await setup.Owner.Client.PostAsync<long>("winget/sync", new { organizationId = setup.OrganizationId, storeAppId });

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    private string Installer(string version, byte[] bytes)
    {
        var url = $"https://download.milvasoft.com/kabuk/{version}/KabukSetup-{Guid.NewGuid():N}.exe";

        GitHub.Downloads[url] = bytes;

        return url;
    }

    private async Task<(HttpResponseMessage Http, Response<ChangePlanDto> Plan)> PlanAsync(Setup setup, int storeAppId, object body)
    {
        var json = JsonSerializer.SerializeToNode(body).AsObject();

        json["organizationId"] = setup.OrganizationId;
        json["storeAppId"] = storeAppId;

        return await setup.Owner.Client.PostAsync<ChangePlanDto>("winget/change", json);
    }

    private async Task<StoreJob> ApproveAsync(Setup setup, ChangePlanDto plan)
    {
        var (http, approved) = await setup.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = setup.OrganizationId, planId = plan.PlanId, confirm = plan.ConfirmText });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", approved.Messages?.Select(m => m.Message) ?? []));
        approved.Data.JobId.Should().NotBeNull();

        return await WaitForJobAsync(approved.Data.JobId.Value);
    }

    [Fact]
    public async Task Connection_ShouldVerifyTheFork_AndKeepReplacementsOnTheSameFork()
    {
        // Arrange
        var setup = await NewSetupAsync(withPackage: false);
        var (otherToken, _, otherFork) = GitHub.AddAccount();

        // Act
        var (_, verified) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });
        var (moveHttp, _) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(otherToken, otherFork) });

        var (newToken, _, _) = GitHub.AddAccount();
        GitHub.Users[newToken] = new FakeGitHub.User(setup.Login, "public_repo");

        var (replaceHttp, replaced) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(newToken, setup.Fork), credentialExpiryDate = DateTime.UtcNow.AddMonths(6) });

        var noScope = GitHub.AddAccount(scopes: "read:user");
        var (_, rejectedConnection) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Winget, name = "no scope", secret = Secret(noScope.Token, noScope.Fork) });
        var (_, rejected) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = rejectedConnection.Data });
        var (invalidHttp, _) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Winget, name = "bad", secret = Secret("not-a-token", setup.Fork) });

        // Assert
        verified.Data.Status.Should().Be(ConnectionStatus.Verified, verified.Data.LastVerifyError);
        verified.Data.Fingerprint.Repository.Should().Be(setup.Fork);
        verified.Data.Fingerprint.AccountLogin.Should().Be(setup.Login);
        verified.Data.Fingerprint.TokenKind.Should().Be("classic");
        JsonSerializer.Serialize(verified.Data).Should().NotContain(setup.Token);

        moveHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replaceHttp.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", replaced.Messages?.Select(m => m.Message) ?? []));
        replaced.Data.CredentialExpiryDate.Should().NotBeNull();

        rejected.Data.Status.Should().Be(ConnectionStatus.Rejected);
        rejected.Data.LastVerifyError.Should().Contain("public_repo");
        invalidHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);

        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Action == AuditAction.ConnectionKeyReplaced).Select(a => a.Summary).ToListAsync());

        audit.Should().ContainSingle().Which.Should().Contain(setup.Fork).And.NotContain(newToken);
    }

    [Fact]
    public async Task Package_ShouldSyncVersionsAndListing_AndStayInsideItsOrganization()
    {
        // Arrange
        var setup = await NewSetupAsync();

        GitHub.AddVersion(setup.PackageId, "1.10.0", Manifests(setup.PackageId, "1.10.0"));

        var outside = GitHub.AddPull(setup.Login, $"New version: {setup.PackageId} version 1.9.0", "Validation-Completed");
        var other = await NewSetupAsync(withPackage: false);

        // Act
        var added = await AddPackageAsync(setup);
        var package = await GetPackageAsync(setup, added.StoreAppId);
        var (_, manifest) = await setup.Owner.Client.GetAsync<WingetManifestDto>($"winget/manifest?organizationId={setup.OrganizationId}&storeAppId={added.StoreAppId}&version=1.2.0");
        var (otherHttp, _) = await other.Owner.Client.GetAsync<WingetPackageDto>($"winget/package?organizationId={other.OrganizationId}&storeAppId={added.StoreAppId}");
        var (duplicateHttp, _) = await setup.Owner.Client.PostAsync<WingetPackageAddedDto>("winget/package", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, packageIdentifier = setup.PackageId });

        // Assert
        package.LatestVersion.Should().Be("1.10.0");
        package.Versions.Should().Equal("1.10.0", "1.2.0");
        package.PackageName.Should().Be("Kabuk");
        package.Publisher.Should().Be("Milvasoft");
        package.Fork.Should().Be(setup.Fork);
        package.IsPublished.Should().BeTrue();
        package.PullRequests.Should().ContainSingle(p => p.Number == outside.Number).Which.State.Should().Be(WingetPullRequestState.ValidationCompleted);

        manifest.Data.Version.Should().Be("1.2.0");
        manifest.Data.Locales.Should().ContainSingle(l => l.Locale == "en-US" && l.IsDefault).Which.Fields["ShortDescription"].Should().Be("A fast terminal.");
        manifest.Data.Installers.Should().ContainSingle().Which.SilentSwitches.Should().Be("/S");
        manifest.Data.Issues.Should().NotContain(i => i.Blocking);

        otherHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        duplicateHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);

        var builds = await _factory.DbAsync(db => db.Builds.Where(b => b.StoreAppId == added.StoreAppId).Select(b => b.Version).ToListAsync());

        builds.Should().BeEquivalentTo(["1.10.0", "1.2.0"]);
    }

    [Fact]
    public async Task NewVersion_ShouldOpenAPullRequestWithComputedHashes_AndFollowItsReviewUntilPublished()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddPackageAsync(setup);
        var bytes = Encoding.UTF8.GetBytes($"installer {Guid.NewGuid()}");
        var url = Installer("1.3.0", bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes));

        // Act: plan (dry run), approve, then the review moves on in winget-pkgs.
        var (planHttp, plan) = await PlanAsync(setup, added.StoreAppId, new
        {
            kind = WingetChangeKind.NewVersion,
            version = "1.3.0",
            installers = new[] { new { url, architecture = "x64", scope = "user" } },
            locales = new[] { new { locale = "en-US", fields = new Dictionary<string, string> { ["ReleaseNotes"] = "Faster startup." } } },
        });

        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", plan.Messages?.Select(m => m.Message) ?? []));

        var job = await ApproveAsync(setup, plan.Data);
        var row = await _factory.DbAsync(db => db.WingetPullRequests.AsNoTracking().SingleAsync(p => p.StoreAppId == added.StoreAppId));
        var pull = GitHub.Pulls[row.Number];

        pull.Labels.AddRange(["Azure-Pipeline-Passed", "Validation-Completed", "Moderator-Approved"]);
        pull.Comments.Add(new JsonObject { ["user"] = new JsonObject { ["login"] = "microsoft-github-policy-service[bot]", ["type"] = "Bot" }, ["body"] = "Validation has completed.", ["created_at"] = "2026-11-22T09:00:00Z" });
        pull.Checks.Add(new JsonObject { ["name"] = "Validation", ["status"] = "completed", ["conclusion"] = "success" });

        await SyncAsync(setup, added.StoreAppId);

        var approved = await _factory.DbAsync(db => db.WingetPullRequests.AsNoTracking().SingleAsync(p => p.Id == row.Id));

        GitHub.Merge(row.Number);
        await SyncAsync(setup, added.StoreAppId);

        var package = await GetPackageAsync(setup, added.StoreAppId);

        // Assert
        plan.Data.Kind.Should().Be(ChangePlanKind.WingetChange);
        plan.Data.Store.Should().Be(StoreKind.Winget);
        plan.Data.ConfirmText.Should().Be($"{setup.PackageId} 1.3.0");
        plan.Data.Items.Should().Contain(i => i.Group == "Manifest" && i.Field.EndsWith($"{setup.PackageId}.installer.yaml", StringComparison.Ordinal));
        plan.Data.Warnings.Should().Contain(w => w.Field == "InstallerSha256");

        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        row.State.Should().Be(WingetPullRequestState.Validating);
        row.CreatedByStoreasy.Should().BeTrue();
        row.PlanId.Should().Be(plan.Data.PlanId);
        row.PackageVersion.Should().Be("1.3.0");

        pull.Title.Should().Be($"New version: {setup.PackageId} version 1.3.0");
        pull.Author.Should().Be(setup.Login);
        pull.Files.Keys.Should().OnlyContain(p => p.Contains("/1.3.0/", StringComparison.Ordinal)).And.HaveCount(3);
        pull.Files.Single(f => f.Key.EndsWith(".installer.yaml", StringComparison.Ordinal)).Value.Should().Contain($"InstallerSha256: {sha}").And.Contain(url).And.Contain("Silent: /S");
        pull.Files.Single(f => f.Key.EndsWith(".locale.en-US.yaml", StringComparison.Ordinal)).Value.Should().Contain("ReleaseNotes: Faster startup.").And.NotContain("Notes of 1.2.0");
        GitHub.AuthorizedHosts.Should().OnlyContain(h => h == "api.github.fake", "the GitHub token never goes to an installer host");

        approved.State.Should().Be(WingetPullRequestState.Approved);
        approved.Comments.Should().ContainSingle().Which.Excerpt.Should().Be("Validation has completed.");
        approved.Checks.Should().ContainSingle().Which.Conclusion.Should().Be("success");
        approved.StatusHistory.Select(h => h.State).Should().Equal(WingetPullRequestState.Validating, WingetPullRequestState.Approved);

        package.PullRequests.Should().ContainSingle().Which.State.Should().Be(WingetPullRequestState.Merged);
        package.LatestVersion.Should().Be("1.3.0");

        var actions = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Store == StoreKind.Winget).Select(a => a.Action).ToListAsync());

        actions.Should().Contain([AuditAction.WingetPackageAdded, AuditAction.WingetPullRequestOpened, AuditAction.WingetReviewStateChanged, AuditAction.WingetPublished]);

        var notifications = await _factory.DbAsync(db => db.InternalNotifications.Where(n => n.OrganizationId == setup.OrganizationId && n.Type == NotificationType.VersionState).CountAsync());

        notifications.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NewVersion_ShouldRefuseDuplicates_AndPushNothingWhenTheHashDoesNotMatch()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddPackageAsync(setup);
        var url = Installer("1.4.0", Encoding.UTF8.GetBytes("real bytes"));

        // Act
        var (existsHttp, _) = await PlanAsync(setup, added.StoreAppId, new { kind = WingetChangeKind.NewVersion, version = "1.2.0", installers = new[] { new { url, architecture = "x64", scope = "user" } } });
        var (_, plan) = await PlanAsync(setup, added.StoreAppId, new { kind = WingetChangeKind.NewVersion, version = "1.4.0", installers = new[] { new { url, architecture = "x64", scope = "user", sha256 = new string('F', 64) } } });
        var job = await ApproveAsync(setup, plan.Data);

        // Assert
        existsHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        job.Status.Should().Be(StoreJobStatus.Failed);
        job.Error.Should().Contain("nothing was pushed");
        GitHub.Forks[setup.Fork].Refs.Should().BeEmpty();
        (await _factory.DbAsync(db => db.WingetPullRequests.CountAsync(p => p.StoreAppId == added.StoreAppId))).Should().Be(0);
    }

    [Fact]
    public async Task NewVersion_ShouldKeepTheBranch_WhenGitHubRefusesThePullRequest()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddPackageAsync(setup);
        var url = Installer("1.5.0", Encoding.UTF8.GetBytes("bytes"));

        GitHub.RefusePulls[setup.Token] = true;

        // Act
        var (_, plan) = await PlanAsync(setup, added.StoreAppId, new { kind = WingetChangeKind.NewVersion, version = "1.5.0", installers = new[] { new { url, architecture = "x64", scope = "user" } } });
        var job = await ApproveAsync(setup, plan.Data);

        // Assert
        job.Status.Should().Be(StoreJobStatus.Failed);
        job.Error.Should().Contain("compare").And.Contain(setup.Login);
        GitHub.Forks[setup.Fork].Refs.Keys.Should().ContainSingle().Which.Should().StartWith($"storeasy/{setup.PackageId}-1.5.0-");
    }

    [Fact]
    public async Task Listing_ShouldPushOnlyTheChangedLocaleManifests()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddPackageAsync(setup);

        // Act
        var (planHttp, plan) = await PlanAsync(setup, added.StoreAppId, new
        {
            kind = WingetChangeKind.Listing,
            locales = new object[]
            {
                new { locale = "en-US", fields = new Dictionary<string, string> { ["ShortDescription"] = "The fastest terminal." } },
                new { locale = "tr-TR", fields = new Dictionary<string, string> { ["ShortDescription"] = "En hızlı terminal." } },
            },
        });

        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", plan.Messages?.Select(m => m.Message) ?? []));

        var job = await ApproveAsync(setup, plan.Data);
        var number = await _factory.DbAsync(db => db.WingetPullRequests.Where(p => p.StoreAppId == added.StoreAppId).Select(p => p.Number).SingleAsync());
        var pull = GitHub.Pulls[number];

        // Assert
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        pull.Title.Should().Be($"Update metadata: {setup.PackageId} version 1.2.0");
        pull.Files.Keys.Select(k => k[(k.LastIndexOf('/') + 1)..]).Should().BeEquivalentTo([$"{setup.PackageId}.locale.en-US.yaml", $"{setup.PackageId}.locale.tr-TR.yaml"]);
        pull.Files.Values.Should().Contain(v => v.Contains("ShortDescription: En hızlı terminal.", StringComparison.Ordinal) && v.Contains("ManifestType: locale", StringComparison.Ordinal));
    }
}
