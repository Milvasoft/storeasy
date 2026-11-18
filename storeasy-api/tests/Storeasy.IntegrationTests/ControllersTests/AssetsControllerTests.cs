using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Presigned uploads and downloads of organization files.")]
public class AssetsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(TestUser Owner, int OrganizationId)> NewOrganizationAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, body) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });

        return (owner, body.Data);
    }

    private static object UploadBody(int organizationId, long size, AssetPurpose purpose = AssetPurpose.Screenshot)
        => new { organizationId, purpose, fileName = "Home Screen 1.png", contentType = "image/png", size, sha256 = new string('a', 64) };

    private Task<string> ObjectKeyAsync(long assetId) => _factory.DbAsync(db => db.Assets.AsNoTracking().Where(a => a.Id == assetId).Select(a => a.ObjectKey).SingleAsync());

    [Fact]
    public async Task SingleUpload_ShouldBeConfirmedAndDownloadable()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();

        // Act
        var (http, upload) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 1234));
        var key = await ObjectKeyAsync(upload.Data.AssetId);

        _factory.FileStorage.Put(key, 1234, "image/png");

        var (completeHttp, completed) = await owner.Client.PostAsync<AssetDto>("assets/asset/complete", new { organizationId, assetId = upload.Data.AssetId });
        var (again, _) = await owner.Client.PostAsync<AssetDto>("assets/asset/complete", new { organizationId, assetId = upload.Data.AssetId });
        var (_, download) = await owner.Client.GetAsync<AssetDownloadDto>($"assets/asset/download-url?organizationId={organizationId}&assetId={upload.Data.AssetId}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        upload.Data.UploadUrl.Should().StartWith(FakeFileStorage.BaseUrl);
        upload.Data.Parts.Should().BeEmpty();
        key.Should().StartWith($"org/{organizationId}/screenshot/").And.EndWith("/Home-Screen-1.png");
        completeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        completed.Data.Status.Should().Be(AssetStatus.Uploaded);
        completed.Data.Size.Should().Be(1234);
        again.StatusCode.Should().Be(HttpStatusCode.OK, "completing twice is retry safe");
        download.Data.Url.Should().Contain(key);
    }

    [Fact]
    public async Task LargeFile_ShouldGetPresignedPartsAndCompleteWithTheirETags()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        const long size = 100L * 1024 * 1024;

        // Act
        var (_, upload) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, size, AssetPurpose.BuildArtifact));
        var key = await ObjectKeyAsync(upload.Data.AssetId);
        var parts = upload.Data.Parts.Select(p => new { partNumber = p.PartNumber, eTag = $"\"etag-{p.PartNumber}\"" }).ToList();

        _factory.FileStorage.Put(key, size);

        var (http, completed) = await owner.Client.PostAsync<AssetDto>("assets/asset/complete", new { organizationId, assetId = upload.Data.AssetId, parts });

        // Assert
        upload.Data.UploadUrl.Should().BeNull();
        upload.Data.PartSize.Should().Be(AssetRules.DefaultPartSize);
        upload.Data.Parts.Select(p => p.PartNumber).Should().Equal(1, 2, 3, 4, 5, 6, 7);
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        completed.Data.Status.Should().Be(AssetStatus.Uploaded);
        _factory.FileStorage.CompletedUploads[key].Should().HaveCount(7);
    }

    [Fact]
    public async Task UploadOfAnotherSize_ShouldBeRejectedAndRemoved()
    {
        var (owner, organizationId) = await NewOrganizationAsync();
        var (_, upload) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 1000));
        var key = await ObjectKeyAsync(upload.Data.AssetId);

        _factory.FileStorage.Put(key, 999_999);

        var (http, _) = await owner.Client.PostAsync<AssetDto>("assets/asset/complete", new { organizationId, assetId = upload.Data.AssetId });
        var status = await _factory.DbAsync(db => db.Assets.AsNoTracking().Where(a => a.Id == upload.Data.AssetId).Select(a => a.Status).SingleAsync());

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        status.Should().Be(AssetStatus.Rejected);
        _factory.FileStorage.Deleted.Should().Contain(key);
    }

    [Fact]
    public async Task CompleteWithoutAnUpload_ShouldBe409AndNotDownloadable()
    {
        var (owner, organizationId) = await NewOrganizationAsync();
        var (_, upload) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 1000));

        var (complete, _) = await owner.Client.PostAsync<AssetDto>("assets/asset/complete", new { organizationId, assetId = upload.Data.AssetId });
        var (download, _) = await owner.Client.GetAsync<AssetDownloadDto>($"assets/asset/download-url?organizationId={organizationId}&assetId={upload.Data.AssetId}");

        complete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        download.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TooLargeFile_ShouldBe400()
    {
        var (owner, organizationId) = await NewOrganizationAsync();

        var (http, _) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, AssetRules.MaxBytes(AssetPurpose.Screenshot) + 1));

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uploads_ShouldCheckRolesAndHideOtherOrganizations()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var (stranger, otherOrganizationId) = await NewOrganizationAsync();
        var viewerUser = await _factory.CreateUserAsync(email: $"v{TestApi.Unique()}@example.com");
        var viewer = await _factory.LoginAsync(viewerUser.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = viewerUser.Email, role = OrgRole.Viewer });
        await viewer.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        var (_, upload) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 10));
        _factory.FileStorage.Put(await ObjectKeyAsync(upload.Data.AssetId), 10);
        await owner.Client.PostAsync<AssetDto>("assets/asset/complete", new { organizationId, assetId = upload.Data.AssetId });

        // Act
        var (viewerUpload, _) = await viewer.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 10));
        var (viewerDownload, _) = await viewer.Client.GetAsync<AssetDownloadDto>($"assets/asset/download-url?organizationId={organizationId}&assetId={upload.Data.AssetId}");
        var (strangerUpload, _) = await stranger.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 10));
        var (crossOrganization, _) = await stranger.Client.GetAsync<AssetDownloadDto>($"assets/asset/download-url?organizationId={otherOrganizationId}&assetId={upload.Data.AssetId}");

        // Assert
        viewerUpload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerDownload.StatusCode.Should().Be(HttpStatusCode.OK);
        strangerUpload.StatusCode.Should().Be(HttpStatusCode.NotFound);
        crossOrganization.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WithoutABucket_ShouldBe503()
    {
        var (owner, organizationId) = await NewOrganizationAsync();
        _factory.FileStorage.IsConfigured = false;

        try
        {
            var (http, _) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", UploadBody(organizationId, 10));

            http.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            _factory.FileStorage.IsConfigured = true;
        }
    }
}
