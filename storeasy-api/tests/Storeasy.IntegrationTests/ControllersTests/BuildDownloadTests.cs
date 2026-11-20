using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Download links of the packages storeasy keeps for builds.")]
public class BuildDownloadTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private async Task<(Org Org, Record Google, long SessionId, long BuildId)> DeliveredBuildAsync()
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.halka";

        _factory.Stores.Google.AddApp(package);

        var (_, google) = await NewAppAsync(org, null, package);

        await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = org.OrganizationId,
            appId = google.AppId,
            rules = new[] { new { platform = ArtifactPlatform.Android, enabled = true, target = DeliveryTarget.PlayTrack, targetId = "internal", targetName = "internal", onWarning = DeliveryWarningAction.Hold } },
        });

        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var (sessionId, _, _) = await UploadAsync(client, google.AppId, "halka-1.0.0-42.aab", PackageFactory.Aab(package, 42));
        var session = await WaitForSessionAsync(sessionId);

        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);

        // The sync reads the bundle back from Play; the version code is the build number.
        var buildId = await _factory.DbAsync(async db =>
        {
            var builds = new[]
            {
                new Build { OrganizationId = org.OrganizationId, StoreAppId = google.StoreAppId, Store = StoreKind.Google, ExternalId = "42", BuildNumber = "42", Version = "1.0.0", SyncedDate = DateTime.UtcNow, UploadedDate = DateTime.UtcNow },
                new Build { OrganizationId = org.OrganizationId, StoreAppId = google.StoreAppId, Store = StoreKind.Google, ExternalId = "41", BuildNumber = "41", Version = "0.9.0", SyncedDate = DateTime.UtcNow, UploadedDate = DateTime.UtcNow.AddDays(-1) },
            };

            db.Builds.AddRange(builds);
            await db.SaveChangesAsync();

            return builds[0].Id;
        });

        return (org, google, sessionId, buildId);
    }

    [Fact]
    public async Task DeliveredBuild_ShouldCarryItsSessionAndGiveAnAuditedDownloadUrl()
    {
        // Arrange
        var (org, google, sessionId, buildId) = await DeliveredBuildAsync();
        var otherBuildId = await _factory.DbAsync(db => db.Builds.AsNoTracking().Where(b => b.StoreAppId == google.StoreAppId && b.ExternalId == "41").Select(b => b.Id).SingleAsync());

        // Act
        var (_, detail) = await org.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={org.OrganizationId}&appId={google.AppId}");
        var (_, list) = await org.Owner.Client.PatchListAsync<BuildDto>("builds", new { organizationId = org.OrganizationId, storeAppId = google.StoreAppId, pageNumber = 1, rowCount = 20 });
        var (http, download) = await org.Owner.Client.GetAsync<BuildDownloadDto>($"builds/download-url?organizationId={org.OrganizationId}&buildId={buildId}");
        var (otherHttp, other) = await org.Owner.Client.GetAsync<BuildDownloadDto>($"builds/download-url?organizationId={org.OrganizationId}&buildId={otherBuildId}");
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.BuildPackageDownloaded).ToListAsync());
        var key = await _factory.DbAsync(db => db.ArtifactSessions.AsNoTracking().Where(s => s.Id == sessionId).Select(s => s.ObjectKey).SingleAsync());

        // Assert
        detail.Data.Builds.Single(b => b.Id == buildId).PackageSessionId.Should().Be(sessionId);
        detail.Data.Builds.Single(b => b.Id == otherBuildId).PackageSessionId.Should().BeNull("version code 41 never came through storeasy");
        list.Data.Single(b => b.Id == buildId).PackageSessionId.Should().Be(sessionId);
        http.StatusCode.Should().Be(HttpStatusCode.OK, download?.MessagesToString());
        download.Data.SessionId.Should().Be(sessionId);
        download.Data.Url.Should().StartWith(FakeFileStorage.BaseUrl).And.Contain(key);
        download.Data.FileName.Should().Be("halka-1.0.0-42.aab");
        download.Data.Size.Should().BeGreaterThan(0);
        otherHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Keys(other).Should().Contain(MessageKey.BuildPackageNotStored);
        audit.Should().ContainSingle();
        audit[0].TargetId.Should().Be(buildId.ToString());
        audit[0].Details.Should().Contain(sessionId.ToString()).And.NotContain(FakeFileStorage.BaseUrl, "the presigned URL is a bearer link");
    }

    [Fact]
    public async Task DeletedPackage_ShouldHaveNoLink()
    {
        var (org, google, sessionId, buildId) = await DeliveredBuildAsync();

        await _factory.DbAsync(db => db.ArtifactSessions.Where(s => s.Id == sessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.PackageDeleted, true)));

        var (_, detail) = await org.Owner.Client.GetAsync<AppDetailDto>($"apps/app?organizationId={org.OrganizationId}&appId={google.AppId}");
        var (http, body) = await org.Owner.Client.GetAsync<BuildDownloadDto>($"builds/download-url?organizationId={org.OrganizationId}&buildId={buildId}");

        detail.Data.Builds.Should().OnlyContain(b => b.PackageSessionId == null);
        http.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Keys(body).Should().Contain(MessageKey.BuildPackageNotStored);
    }

    [Fact]
    public async Task DownloadUrl_ShouldBeOpenToViewersAndHiddenFromOtherOrganizations()
    {
        // Arrange
        var (org, _, _, buildId) = await DeliveredBuildAsync();
        var stranger = await NewOrgAsync();
        var viewerUser = await _factory.CreateUserAsync(email: $"v{TestApi.Unique()}@example.com");
        var viewer = await _factory.LoginAsync(viewerUser.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await org.Owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = org.OrganizationId, email = viewerUser.Email, role = OrgRole.Viewer });
        await viewer.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        // Act
        var (viewerHttp, _) = await viewer.Client.GetAsync<BuildDownloadDto>($"builds/download-url?organizationId={org.OrganizationId}&buildId={buildId}");
        var (strangerHttp, _) = await stranger.Owner.Client.GetAsync<BuildDownloadDto>($"builds/download-url?organizationId={org.OrganizationId}&buildId={buildId}");
        var (ownOrgHttp, _) = await stranger.Owner.Client.GetAsync<BuildDownloadDto>($"builds/download-url?organizationId={stranger.OrganizationId}&buildId={buildId}");

        // Assert
        viewerHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        strangerHttp.StatusCode.Should().Be(HttpStatusCode.NotFound, "a non member gets 404 for the organization");
        ownOrgHttp.StatusCode.Should().Be(HttpStatusCode.NotFound, "a build of another organization is not found");
    }
}
