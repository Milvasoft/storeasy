using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using V = Storeasy.IntegrationTests.TestBase.Fakes.FakeRegistries.Version;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Package registries releases and operations: credentials, release settings and detection, release plans with their exact changes, typed approval, the apply job (commit, tag, dispatch), following the CI run and the registry, and registry operations with stored keys; against a fake GitHub and fake registries.")]
public class RegistriesReleaseControllerTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private const string _packageJson = "{\n  \"name\": \"NAME\",\n  \"version\": \"1.0.0\",\n  \"dependencies\": { \"left-pad\": \"1.0.0\" }\n}\n";

    private static readonly DateTime _day = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private FakeRegistries Registries => _factory.Registries;

    private FakeRegistryGitHub GitHub => _factory.RegistryGitHub;

    private static string NewRepository() => $"acme/{FakeRegistries.NewName("repo")}";

    private async Task<int> AddAsync(Org org, PackageRegistry registry, string name)
    {
        var (http, added) = await org.Owner.Client.PostAsync<TrackedPackageAddedDto>("registries/package", new { organizationId = org.OrganizationId, registry, name });

        http.StatusCode.Should().Be(HttpStatusCode.OK, added.MessagesToString());
        (await WaitForJobAsync(added.Data.JobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        return added.Data.Id;
    }

    private static async Task ConnectAsync(Org org, RegistryConnectionKind kind, string secret, DateTime? expiry = null)
    {
        var (http, saved) = await org.Owner.Client.PostAsync<int>("registries/connection", new { organizationId = org.OrganizationId, kind, secret, expiryDate = expiry });

        http.StatusCode.Should().Be(HttpStatusCode.OK, saved.MessagesToString());
    }

    private static async Task SaveSettingsAsync(Org org, object scope, ReleaseSettings settings)
    {
        var body = new Dictionary<string, object> { ["organizationId"] = org.OrganizationId, ["settings"] = settings };

        foreach (var property in scope.GetType().GetProperties())
            body[property.Name] = property.GetValue(scope);

        var (http, saved) = await org.Owner.Client.PutAsync<int>("registries/release", body);

        http.StatusCode.Should().Be(HttpStatusCode.OK, saved.MessagesToString());
    }

    private async Task<RegistryPlanDto> ApproveAsync(Org org, RegistryPlanDto plan)
    {
        var (http, approved) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/plan/approve", new { organizationId = org.OrganizationId, planId = plan.Id, confirm = plan.ConfirmText });

        http.StatusCode.Should().Be(HttpStatusCode.OK, approved.MessagesToString());
        await WaitForJobAsync(approved.Data.StoreJobId!.Value);

        var (_, read) = await org.Owner.Client.GetAsync<RegistryPlanDto>($"registries/plan?organizationId={org.OrganizationId}&planId={plan.Id}");

        return read.Data;
    }

    private async Task<int> FollowAsync()
    {
        using var scope = _factory.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IRegistryPlanService>().FollowAsync(CancellationToken.None);
    }

    private Task<List<AuditAction>> AuditAsync(Org org)
        => _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());

    [Fact]
    public async Task Credentials_ShouldBeSealed_ShownByFingerprint_AndKeptPerOrganization()
    {
        // Arrange
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        const string nugetKey = "oy2nugetfakekey0000000000000000000000000000wxyz";
        var expiry = DateTime.UtcNow.Date.AddDays(60);

        // Act
        await ConnectAsync(org, RegistryConnectionKind.GitHub, FakeRegistryGitHub.Token);
        await ConnectAsync(org, RegistryConnectionKind.NuGet, nugetKey, expiry);
        await ConnectAsync(other, RegistryConnectionKind.GitHub, "github_pat_wrong_0000000000000000");
        var (listHttp, list) = await org.Owner.Client.GetAsync<List<RegistryConnectionDto>>($"registries/connections?organizationId={org.OrganizationId}");
        var (rawHttp, _) = await org.Owner.Client.GetAsync<object>($"registries/connections?organizationId={org.OrganizationId}");
        var raw = await rawHttp.Content.ReadAsStringAsync();
        var (_, otherList) = await other.Owner.Client.GetAsync<List<RegistryConnectionDto>>($"registries/connections?organizationId={other.OrganizationId}");
        var sealedSecret = await _factory.DbAsync(db => db.RegistryConnections.Where(c => c.OrganizationId == org.OrganizationId && c.Kind == RegistryConnectionKind.NuGet).Select(c => c.EncryptedSecret).SingleAsync());
        var reminder = await _factory.DbAsync(db => db.RegistryTokenReminders.Where(r => r.OrganizationId == org.OrganizationId).Select(r => new { r.Registry, r.ExpiryDate }).SingleAsync());
        var nugetId = list.Data.Single(c => c.Kind == RegistryConnectionKind.NuGet).Id;
        var (foreignRemove, _) = await other.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/connection", new { organizationId = other.OrganizationId, id = nugetId });
        var (tokenLikeHttp, _) = await org.Owner.Client.PostAsync<int>("registries/connection", new { organizationId = org.OrganizationId, kind = RegistryConnectionKind.Npm, secret = "has space inside" });
        var (removeHttp, _) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "registries/connection", new { organizationId = org.OrganizationId, id = nugetId });
        var remindersLeft = await _factory.DbAsync(db => db.RegistryTokenReminders.CountAsync(r => r.OrganizationId == org.OrganizationId));

        // Assert
        listHttp.StatusCode.Should().Be(HttpStatusCode.OK, list.MessagesToString());
        list.Data.Should().HaveCount(2);
        list.Data.Single(c => c.Kind == RegistryConnectionKind.GitHub).Should().BeEquivalentTo(new { Fingerprint = "octocat", Status = ConnectionStatus.Verified }, o => o.ExcludingMissingMembers());
        list.Data.Single(c => c.Kind == RegistryConnectionKind.NuGet).Should().BeEquivalentTo(new { Fingerprint = "****wxyz", ExpiryDate = (DateTime?)expiry }, o => o.ExcludingMissingMembers());
        raw.Should().NotContain(nugetKey).And.NotContain(FakeRegistryGitHub.Token);
        System.Text.Encoding.UTF8.GetString(sealedSecret).Should().NotContain(nugetKey);
        reminder.Registry.Should().Be(PackageRegistry.NuGet);
        reminder.ExpiryDate.Should().Be(expiry);
        otherList.Data.Single().Status.Should().Be(ConnectionStatus.Rejected, "GitHub refused the token");
        foreignRemove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        tokenLikeHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        removeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        remindersLeft.Should().Be(0, "the key's reminder goes with it");
        (await AuditAsync(org)).Should().Contain([AuditAction.RegistryConnectionSaved, AuditAction.RegistryConnectionRemoved]);
    }

    [Fact]
    public async Task VersionBump_ShouldPlanTheExactChange_NeedTheTypedApproval_CommitAndFollowToPublished()
    {
        // Arrange
        var org = await NewOrgAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);
        var name = FakeRegistries.NewName("rel");
        var repository = NewRepository();

        Registries.SetNpm(name, [new V("1.0.0", _day)]);
        var packageId = await AddAsync(org, PackageRegistry.Npm, name);
        GitHub.SetFile(repository, "master", "package.json", _packageJson.Replace("NAME", name, StringComparison.Ordinal));
        await ConnectAsync(org, RegistryConnectionKind.GitHub, FakeRegistryGitHub.Token);
        await SaveSettingsAsync(org, new { trackedPackageId = packageId }, new ReleaseSettings { Repository = repository, Trigger = ReleaseTrigger.VersionBump, Workflow = "npm-publish.yml", VersionFiles = [new VersionFileSetting { Path = "package.json", Format = VersionFileFormat.PackageJson }] });

        // Act
        var (_, settings) = await org.Owner.Client.GetAsync<ReleaseSettingsDto>($"registries/release?organizationId={org.OrganizationId}&trackedPackageId={packageId}&prerelease=rc.1");
        var (lowerHttp, _) = await editor.Client.PostAsync<RegistryPlanDto>("registries/release/plan", new { organizationId = org.OrganizationId, trackedPackageId = packageId, version = "0.9.0" });
        var (planHttp, plan) = await editor.Client.PostAsync<RegistryPlanDto>("registries/release/plan", new { organizationId = org.OrganizationId, trackedPackageId = packageId, version = "1.0.1" });
        var (editorApprove, _) = await editor.Client.PostAsync<RegistryPlanDto>("registries/plan/approve", new { organizationId = org.OrganizationId, planId = plan.Data.Id, confirm = plan.Data.ConfirmText });
        var (wrongConfirm, _) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/plan/approve", new { organizationId = org.OrganizationId, planId = plan.Data.Id, confirm = name });
        var (foreignRead, _) = await (await NewOrgAsync()).Owner.Client.GetAsync<RegistryPlanDto>($"registries/plan?organizationId={org.OrganizationId}&planId={plan.Data.Id}");
        var untouched = GitHub.GetFile(repository, "master", "package.json");
        var applied = await ApproveAsync(org, plan.Data);
        var runId = GitHub.AddRun(repository, applied.CommitSha, "npm-publish.yml");
        var stillRunning = await FollowAsync();
        var (_, following) = await org.Owner.Client.GetAsync<RegistryPlanDto>($"registries/plan?organizationId={org.OrganizationId}&planId={plan.Data.Id}");
        GitHub.SetRun(runId, "completed", "success");
        Registries.SetNpm(name, [new V("1.0.0", _day), new V("1.0.1", DateTime.UtcNow)]);
        await FollowAsync();
        var (_, published) = await org.Owner.Client.GetAsync<RegistryPlanDto>($"registries/plan?organizationId={org.OrganizationId}&planId={plan.Data.Id}");
        var (_, plans) = await org.Owner.Client.GetAsync<List<RegistryPlanDto>>($"registries/plans?organizationId={org.OrganizationId}&trackedPackageId={packageId}");

        // Assert
        settings.Data.CurrentVersion.Should().Be("1.0.0");
        settings.Data.GitHubConnected.Should().BeTrue();
        settings.Data.NextVersions.Select(v => v.Version).Should().Equal("1.0.1", "1.1.0", "2.0.0", "1.0.1-rc.1");
        lowerHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the new version must be higher");
        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, plan.MessagesToString());
        plan.Data.Should().BeEquivalentTo(new { ConfirmText = $"{name} 1.0.1", RequiredPermission = OrgPermission.ReleaseProduction, Status = ChangePlanStatus.Pending, CurrentVersion = "1.0.0" }, o => o.ExcludingMissingMembers());
        plan.Data.Files.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Path = "package.json", OldVersion = "1.0.0", Line = 3, Before = "  \"version\": \"1.0.0\",", After = "  \"version\": \"1.0.1\"," }, o => o.ExcludingMissingMembers());
        editorApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden, "an editor has no production release permission");
        wrongConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreignRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        untouched.Should().Contain("\"version\": \"1.0.0\"", "nothing is written before the approval");
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        applied.FollowStatus.Should().Be(ReleaseFollowStatus.Following);
        applied.CommitSha.Should().Be(GitHub.Head(repository, "master"));
        GitHub.GetFile(repository, "master", "package.json").Should().Contain("\"version\": \"1.0.1\"").And.Contain("\"left-pad\": \"1.0.0\"");
        stillRunning.Should().BeGreaterThanOrEqualTo(0);
        following.Data.FollowStatus.Should().Be(ReleaseFollowStatus.Following);
        following.Data.RunUrl.Should().EndWith($"/actions/runs/{runId}");
        published.Data.FollowStatus.Should().Be(ReleaseFollowStatus.Published);
        published.Data.RunConclusion.Should().Be("success");
        plans.Data.Should().ContainSingle(p => p.Id == plan.Data.Id);
        (await AuditAsync(org)).Should().Contain([AuditAction.RegistryReleaseSettingsSaved, AuditAction.RegistryPlanCreated, AuditAction.RegistryPlanApproved, AuditAction.RegistryReleaseStarted, AuditAction.RegistryReleasePublished]);
    }

    [Fact]
    public async Task Approval_ShouldBeRefused_WhenTheVersionFileChangedAfterThePlan()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("chg");
        var repository = NewRepository();

        Registries.SetNpm(name, [new V("1.0.0", _day)]);
        var packageId = await AddAsync(org, PackageRegistry.Npm, name);
        GitHub.SetFile(repository, "master", "package.json", _packageJson);
        await ConnectAsync(org, RegistryConnectionKind.GitHub, FakeRegistryGitHub.Token);
        await SaveSettingsAsync(org, new { trackedPackageId = packageId }, new ReleaseSettings { Repository = repository, Trigger = ReleaseTrigger.VersionBump, VersionFiles = [new VersionFileSetting { Path = "package.json", Format = VersionFileFormat.PackageJson }] });
        var (_, plan) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/release/plan", new { organizationId = org.OrganizationId, trackedPackageId = packageId, version = "1.1.0" });

        // Act
        GitHub.SetFile(repository, "master", "package.json", _packageJson.Replace("1.0.0\",\n  \"dep", "1.0.5\",\n  \"dep", StringComparison.Ordinal));
        var (http, refused) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/plan/approve", new { organizationId = org.OrganizationId, planId = plan.Data.Id, confirm = plan.Data.ConfirmText });
        var (rejectHttp, rejected) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/plan/reject", new { organizationId = org.OrganizationId, planId = plan.Data.Id });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.Conflict, refused.MessagesToString());
        rejectHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        rejected.Data.Status.Should().Be(ChangePlanStatus.Rejected);
        GitHub.GetFile(repository, "master", "package.json").Should().Contain("1.0.5");
    }

    [Fact]
    public async Task GroupRelease_ShouldBumpSeveralFilesInOneCommit_TagIt_AndReportAFailedRun()
    {
        // Arrange
        var org = await NewOrgAsync();
        var first = FakeRegistries.NewName("ga");
        var second = FakeRegistries.NewName("gb");
        var repository = NewRepository();

        Registries.SetNpm(first, [new V("2.0.0", _day)]);
        Registries.SetNpm(second, [new V("1.9.0", _day)]);
        var firstId = await AddAsync(org, PackageRegistry.Npm, first);
        var secondId = await AddAsync(org, PackageRegistry.Npm, second);
        var (_, group) = await org.Owner.Client.PostAsync<int>("registries/group", new { organizationId = org.OrganizationId, name = "SDKs " + first, trackedPackageIds = new[] { firstId, secondId } });
        GitHub.SetFile(repository, "master", "sdks/a/package.json", _packageJson.Replace("1.0.0\",\n  \"dep", "2.0.0\",\n  \"dep", StringComparison.Ordinal));
        GitHub.SetFile(repository, "master", "sdks/b/package.json", _packageJson.Replace("1.0.0\",\n  \"dep", "1.9.0\",\n  \"dep", StringComparison.Ordinal));
        await ConnectAsync(org, RegistryConnectionKind.GitHub, FakeRegistryGitHub.Token);
        await SaveSettingsAsync(org, new { packageGroupId = group.Data }, new ReleaseSettings
        {
            Repository = repository,
            Trigger = ReleaseTrigger.VersionBumpAndTag,
            TagPattern = "sdk-v{version}",
            Workflow = "sdk-publish.yml",
            VersionFiles = [new VersionFileSetting { Path = "sdks/a/package.json", Format = VersionFileFormat.PackageJson }, new VersionFileSetting { Path = "sdks/b/package.json", Format = VersionFileFormat.PackageJson }],
        });

        // Act
        var (_, fromGroup) = await org.Owner.Client.GetAsync<ReleaseSettingsDto>($"registries/release?organizationId={org.OrganizationId}&trackedPackageId={secondId}");
        var (planHttp, plan) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/release/plan", new { organizationId = org.OrganizationId, packageGroupId = group.Data, version = "2.1.0" });
        var applied = await ApproveAsync(org, plan.Data);
        var runId = GitHub.AddRun(repository, applied.CommitSha, "sdk-publish.yml", "completed", "failure");
        GitHub.Tags[$"{repository}|sdk-v2.1.1"] = applied.CommitSha;
        await FollowAsync();
        var (_, failed) = await org.Owner.Client.GetAsync<RegistryPlanDto>($"registries/plan?organizationId={org.OrganizationId}&planId={plan.Data.Id}");
        var (againHttp, _) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/release/plan", new { organizationId = org.OrganizationId, packageGroupId = group.Data, version = "2.1.1" });

        // Assert
        fromGroup.Data.FromGroup.Should().BeTrue();
        fromGroup.Data.Settings.TagPattern.Should().Be("sdk-v{version}");
        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, plan.MessagesToString());
        plan.Data.Tag.Should().Be("sdk-v2.1.0");
        plan.Data.PackageIds.Should().BeEquivalentTo([firstId, secondId]);
        plan.Data.Files.Select(f => f.OldVersion).Should().Equal("2.0.0", "1.9.0");
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        GitHub.GetFile(repository, "master", "sdks/a/package.json").Should().Contain("\"version\": \"2.1.0\"");
        GitHub.GetFile(repository, "master", "sdks/b/package.json").Should().Contain("\"version\": \"2.1.0\"");
        GitHub.Tags[$"{repository}|sdk-v2.1.0"].Should().Be(applied.CommitSha, "the tag points at the bump commit");
        failed.Data.FollowStatus.Should().Be(ReleaseFollowStatus.Failed);
        failed.Data.RunUrl.Should().EndWith($"/actions/runs/{runId}");
        failed.Data.Error.Should().Contain("failure");
        againHttp.StatusCode.Should().Be(HttpStatusCode.Conflict, "the tag sdk-v2.1.1 already exists");

        (await AuditAsync(org)).Should().Contain(AuditAction.RegistryReleaseFailed);
    }

    [Fact]
    public async Task Dispatch_ShouldPassTheInputs_AndDetectionShouldSuggestFromTheWorkflows()
    {
        // Arrange
        var org = await NewOrgAsync();
        var name = FakeRegistries.NewName("dsp");
        var repository = NewRepository();

        Registries.SetNpm(name, [new V("10.0.11", _day)]);
        var packageId = await AddAsync(org, PackageRegistry.Npm, name);
        await _factory.DbAsync(db => db.TrackedPackages.Where(p => p.Id == packageId).ExecuteUpdateAsync(s => s.SetProperty(p => p.SourceRepository, $"https://github.com/{repository}")));
        GitHub.SetFile(repository, "master", ".github/workflows/release.yml", """
            on:
              push:
                tags:
                  - 'v*.*.*'
              workflow_dispatch:
                inputs:
                  version:
                    required: true
                  prerelease:
                    type: boolean
            jobs:
              release:
                steps:
                  - run: npm publish --provenance
            """);
        await ConnectAsync(org, RegistryConnectionKind.GitHub, FakeRegistryGitHub.Token);

        // Act
        var (detectHttp, suggestions) = await org.Owner.Client.GetAsync<List<ReleaseSuggestionDto>>($"registries/release/detect?organizationId={org.OrganizationId}&trackedPackageId={packageId}");
        var dispatch = suggestions.Data.Single(s => s.Settings.Trigger == ReleaseTrigger.Dispatch).Settings;
        await SaveSettingsAsync(org, new { trackedPackageId = packageId }, dispatch);
        var (_, plan) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/release/plan", new { organizationId = org.OrganizationId, trackedPackageId = packageId, version = "10.0.12-rc.1" });
        var applied = await ApproveAsync(org, plan.Data);

        // Assert
        detectHttp.StatusCode.Should().Be(HttpStatusCode.OK, suggestions.MessagesToString());
        suggestions.Data.Should().Contain(s => s.Settings.Trigger == ReleaseTrigger.Tag && s.Settings.TagPattern == "v{version}");
        dispatch.Should().BeEquivalentTo(new { Repository = repository, Workflow = "release.yml", DispatchVersionInput = "version", DispatchPrereleaseInput = "prerelease" }, o => o.ExcludingMissingMembers());
        plan.Data.DispatchInputs.Should().BeEquivalentTo(new Dictionary<string, string> { ["version"] = "10.0.12-rc.1", ["prerelease"] = "true" });
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        applied.RunId.Should().NotBeNull();
        applied.RunUrl.Should().Contain("/actions/runs/");
        GitHub.Dispatches.Should().Contain(d => d.StartsWith($"{repository}|release.yml|master|", StringComparison.Ordinal) && d.Contains("10.0.12-rc.1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Operations_ShouldUseTheStoredKey_OnlyAfterApproval()
    {
        // Arrange
        var org = await NewOrgAsync();
        var nuget = "Acme." + FakeRegistries.NewName("Op").Replace("-", ".", StringComparison.Ordinal);
        var npm = FakeRegistries.NewName("op");
        var pypi = FakeRegistries.NewName("op");
        const string nugetKey = "oy2operationsfakekey000000000000000000000000abcd";
        const string npmToken = "npm_fakeoperationstoken000000000000000000";

        Registries.SetNuGet(nuget, [new V("1.0.0", _day), new V("1.1.0", _day.AddDays(1))]);
        Registries.SetNpm(npm, [new V("3.0.0", _day)]);
        Registries.SetPyPI(pypi, [new V("1.0.0", _day)]);
        var nugetId = await AddAsync(org, PackageRegistry.NuGet, nuget);
        var npmId = await AddAsync(org, PackageRegistry.Npm, npm);
        var pypiId = await AddAsync(org, PackageRegistry.PyPI, pypi);

        // Act
        var (noKeyHttp, _) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/operation/plan", new { organizationId = org.OrganizationId, trackedPackageId = nugetId, kind = RegistryPlanKind.Unlist, version = "1.0.0" });
        await ConnectAsync(org, RegistryConnectionKind.NuGet, nugetKey);
        await ConnectAsync(org, RegistryConnectionKind.Npm, npmToken);
        var (unknownHttp, _) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/operation/plan", new { organizationId = org.OrganizationId, trackedPackageId = nugetId, kind = RegistryPlanKind.Unlist, version = "9.9.9" });
        var (pypiHttp, _) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/operation/plan", new { organizationId = org.OrganizationId, trackedPackageId = pypiId, kind = RegistryPlanKind.Yank, version = "1.0.0" });
        var (_, unlist) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/operation/plan", new { organizationId = org.OrganizationId, trackedPackageId = nugetId, kind = RegistryPlanKind.Unlist, version = "1.0.0" });
        var (_, deprecate) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/operation/plan", new { organizationId = org.OrganizationId, trackedPackageId = npmId, kind = RegistryPlanKind.Deprecate, version = "3.0.0", argument = "Use 4.x" });
        var writesBefore = Registries.Writes.Count(w => w.Contains(nuget.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) || w.Contains(npm, StringComparison.Ordinal));
        var unlisted = await ApproveAsync(org, unlist.Data);
        var deprecated = await ApproveAsync(org, deprecate.Data);
        var writes = Registries.Writes.Where(w => w.Contains(nuget, StringComparison.OrdinalIgnoreCase) || w.Contains(npm, StringComparison.Ordinal)).ToList();

        // Assert
        noKeyHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "there is no NuGet key yet");
        unknownHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        pypiHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "PyPI has no yank API");
        unlist.Data.ConfirmText.Should().Be($"{nuget} 1.0.0");
        writesBefore.Should().Be(0, "nothing is written before the approval");
        unlisted.Status.Should().Be(ChangePlanStatus.Applied);
        deprecated.Status.Should().Be(ChangePlanStatus.Applied);
        writes.Should().Contain(w => w.StartsWith($"DELETE {FakeRegistries.NuGetPublishUrl}/{nuget}/1.0.0|X-NuGet-ApiKey {nugetKey}|", StringComparison.Ordinal));
        writes.Should().Contain(w => w.StartsWith($"PUT {FakeRegistries.NpmBaseUrl}/{npm}|Bearer {npmToken}|", StringComparison.Ordinal) && w.Contains("\"deprecated\":\"Use 4.x\"", StringComparison.Ordinal));
        System.Text.Json.JsonSerializer.Serialize(unlisted).Should().NotContain(nugetKey);
        (await AuditAsync(org)).Should().Contain(AuditAction.RegistryOperationApplied);
    }

    [Fact]
    public async Task Operation_ShouldFailThePlan_WhenTheRegistryRefusesTheKey()
    {
        // Arrange
        var org = await NewOrgAsync();
        var crate = FakeRegistries.NewName("yk").Replace('-', '_');

        Registries.SetCrate(crate, [new V("0.1.0", _day)]);
        var crateId = await AddAsync(org, PackageRegistry.Crates, crate);
        await ConnectAsync(org, RegistryConnectionKind.Crates, "cio_fakecratestoken00000000000000000");
        var (_, plan) = await org.Owner.Client.PostAsync<RegistryPlanDto>("registries/operation/plan", new { organizationId = org.OrganizationId, trackedPackageId = crateId, kind = RegistryPlanKind.Yank, version = "0.1.0" });

        // Act
        Registries.WriteStatus = HttpStatusCode.Forbidden;
        RegistryPlanDto failed;

        try
        {
            failed = await ApproveAsync(org, plan.Data);
        }
        finally
        {
            Registries.WriteStatus = HttpStatusCode.OK;
        }

        // Assert
        failed.Status.Should().Be(ChangePlanStatus.Failed);
        failed.Error.Should().Contain("403");
        Registries.Writes.Should().Contain(w => w.StartsWith($"DELETE {FakeRegistries.CratesBaseUrl}/crates/{crate}/0.1.0/yank|", StringComparison.Ordinal));
    }
}
