using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using SkiaSharp;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Net.Http.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Organization logo upload, replace and removal.")]
public class OrganizationLogoTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(TestUser Owner, int OrganizationId)> NewOrganizationAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, body) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });

        return (owner, body.Data);
    }

    private async Task<TestUser> AddMemberAsync(TestUser owner, int organizationId, OrgRole role)
    {
        var user = await _factory.CreateUserAsync(email: $"m{TestApi.Unique()}@example.com");
        var member = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = user.Email, role });

        await member.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        return member;
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(0x46, 0x36, 0xFF));
        canvas.Flush();

        using var data = bitmap.Encode(format, 90);

        return data.ToArray();
    }

    private static async Task<(HttpResponseMessage Http, Response<int> Body)> RemoveAsync(HttpClient client, int organizationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{TestApi.Base}/organizations/logo") { Content = JsonContent.Create(new { organizationId }, options: TestApi.Json) };
        var http = await client.SendAsync(request);

        return (http, await TestApi.ReadAsync<Response<int>>(http));
    }

    private Task<string> ObjectKeyAsync(long assetId) => _factory.DbAsync(db => db.Assets.AsNoTracking().Where(a => a.Id == assetId).Select(a => a.ObjectKey).SingleAsync());

    private Task<string> LogoKeyAsync(int organizationId) => _factory.DbAsync(db => db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.LogoKey).SingleAsync());

    /// <summary>
    /// Declares <paramref name="content"/> as <paramref name="contentType"/>, puts it in the fake bucket and confirms it.
    /// </summary>
    private async Task<(HttpResponseMessage Http, Response<string> Body, long AssetId, string UploadKey)> UploadLogoAsync(TestUser user, int organizationId, byte[] content, string contentType = "image/png", long? declaredSize = null)
    {
        var (uploadHttp, upload) = await user.Client.PostAsync<AssetUploadDto>("organizations/logo/upload", new { organizationId, contentType, size = declaredSize ?? content.Length });

        uploadHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        var key = await ObjectKeyAsync(upload.Data.AssetId);

        _factory.FileStorage.Put(key, content, contentType);

        var (http, body) = await user.Client.PutAsync<string>("organizations/logo", new { organizationId, assetId = upload.Data.AssetId });

        return (http, body, upload.Data.AssetId, key);
    }

    [Fact]
    public async Task Owner_ShouldSetALogoThatTheOrganizationListShows()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();

        // Act
        var (http, body, assetId, uploadKey) = await UploadLogoAsync(owner, organizationId, Image(600, 400, SKEncodedImageFormat.Jpeg), "image/jpeg");
        var logoKey = await LogoKeyAsync(organizationId);
        var (_, mine) = await owner.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");
        var uploadLeft = await _factory.DbAsync(db => db.Assets.AnyAsync(a => a.Id == assetId));
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == organizationId).Select(a => a.Action).ToListAsync());

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        logoKey.Should().StartWith($"org/{organizationId}/logo/").And.EndWith(".png");
        body.Data.Should().StartWith(FakeFileStorage.BaseUrl).And.Contain(logoKey);
        mine.Data.Single(o => o.Id == organizationId).LogoUrl.Should().Contain(logoKey);

        using var stored = SKBitmap.Decode(_factory.FileStorage.Content(logoKey));
        stored.Width.Should().Be(OrganizationLogoRules.OutputSize);
        stored.Height.Should().Be(OrganizationLogoRules.OutputSize);

        uploadLeft.Should().BeFalse("the upload is only a ticket; the redrawn copy is kept");
        _factory.FileStorage.Deleted.Should().Contain(uploadKey);
        audit.Should().Contain(AuditAction.OrganizationLogoSet);
    }

    [Fact]
    public async Task NewLogo_ShouldReplaceAndDeleteTheOldOne()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        await UploadLogoAsync(owner, organizationId, Image(64, 64));
        var firstKey = await LogoKeyAsync(organizationId);

        // Act
        var (http, _, _, _) = await UploadLogoAsync(owner, organizationId, Image(300, 300, SKEncodedImageFormat.Webp), "image/webp");
        var secondKey = await LogoKeyAsync(organizationId);

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        secondKey.Should().NotBe(firstKey, "a new key changes the URL, so no cache shows the old logo");
        _factory.FileStorage.Deleted.Should().Contain(firstKey);
    }

    [Fact]
    public async Task Remove_ShouldClearTheLogoAndBeRetrySafe()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        await UploadLogoAsync(owner, organizationId, Image(64, 64));
        var logoKey = await LogoKeyAsync(organizationId);

        // Act
        var (http, _) = await RemoveAsync(owner.Client, organizationId);
        var (again, _) = await RemoveAsync(owner.Client, organizationId);
        var (_, mine) = await owner.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");
        var removals = await _factory.DbAsync(db => db.StoreAuditLogs.CountAsync(a => a.OrganizationId == organizationId && a.Action == AuditAction.OrganizationLogoRemoved));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LogoKeyAsync(organizationId)).Should().BeNull();
        mine.Data.Single(o => o.Id == organizationId).LogoUrl.Should().BeNull();
        _factory.FileStorage.Deleted.Should().Contain(logoKey);
        removals.Should().Be(1, "removing when there is no logo writes nothing");
    }

    [Fact]
    public async Task FileThatIsNotTheDeclaredImage_ShouldBeRefusedAndDeleted()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

        // Act
        var (svgAsPng, body, assetId, uploadKey) = await UploadLogoAsync(owner, organizationId, svg);
        var (jpegAsPng, _, _, _) = await UploadLogoAsync(owner, organizationId, Image(64, 64, SKEncodedImageFormat.Jpeg), "image/png");
        var uploadLeft = await _factory.DbAsync(db => db.Assets.AnyAsync(a => a.Id == assetId));

        // Assert
        svgAsPng.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Messages.Should().Contain(m => m.Key == MessageKey.OrganizationLogoInvalid);
        jpegAsPng.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the signature must match the declared type");
        uploadLeft.Should().BeFalse();
        _factory.FileStorage.Deleted.Should().Contain(uploadKey);
        (await LogoKeyAsync(organizationId)).Should().BeNull();
    }

    [Fact]
    public async Task UploadOfAnotherSize_ShouldBeRefused()
    {
        var (owner, organizationId) = await NewOrganizationAsync();
        var content = Image(64, 64);

        var (http, body, _, _) = await UploadLogoAsync(owner, organizationId, content, declaredSize: content.Length + 10);

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Messages.Should().Contain(m => m.Key == MessageKey.AssetSizeMismatch);
    }

    [Theory]
    [InlineData("image/svg+xml", 1000)]
    [InlineData("image/gif", 1000)]
    [InlineData("image/png", 2L * 1024 * 1024 + 1)]
    [InlineData("image/png", 0)]
    public async Task UploadDeclaration_ShouldRefuseOtherTypesAndLargeFiles(string contentType, long size)
    {
        var (owner, organizationId) = await NewOrganizationAsync();

        var (http, _) = await owner.Client.PostAsync<AssetUploadDto>("organizations/logo/upload", new { organizationId, contentType, size });

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenericAssetUpload_ShouldNotTakeTheLogoPurpose()
    {
        var (owner, organizationId) = await NewOrganizationAsync();

        var (http, _) = await owner.Client.PostAsync<AssetUploadDto>("assets/upload", new { organizationId, purpose = AssetPurpose.OrganizationLogo, fileName = "logo.png", contentType = "image/png", size = 100 });

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(OrgRole.Admin)]
    [InlineData(OrgRole.Editor)]
    [InlineData(OrgRole.Viewer)]
    public async Task OnlyOwners_ShouldChangeTheLogo(OrgRole role)
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var member = await AddMemberAsync(owner, organizationId, role);
        var (_, upload) = await owner.Client.PostAsync<AssetUploadDto>("organizations/logo/upload", new { organizationId, contentType = "image/png", size = 100 });

        // Act
        var (declare, _) = await member.Client.PostAsync<AssetUploadDto>("organizations/logo/upload", new { organizationId, contentType = "image/png", size = 100 });
        var (set, _) = await member.Client.PutAsync<string>("organizations/logo", new { organizationId, assetId = upload.Data.AssetId });
        var (remove, _) = await RemoveAsync(member.Client, organizationId);
        var (_, mine) = await member.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");

        // Assert
        declare.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        set.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        remove.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mine.Data.Should().Contain(o => o.Id == organizationId, "every member still sees the organization and its logo");
    }

    [Fact]
    public async Task AnotherOrganizationsUpload_ShouldBe404AndItsLogoUntouched()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var (stranger, otherOrganizationId) = await NewOrganizationAsync();
        var (_, upload) = await owner.Client.PostAsync<AssetUploadDto>("organizations/logo/upload", new { organizationId, contentType = "image/png", size = 100 });

        // Act
        var (otherOrganization, _) = await stranger.Client.PutAsync<string>("organizations/logo", new { organizationId = otherOrganizationId, assetId = upload.Data.AssetId });
        var (notMember, _) = await stranger.Client.PutAsync<string>("organizations/logo", new { organizationId, assetId = upload.Data.AssetId });
        var (notMemberRemove, _) = await RemoveAsync(stranger.Client, organizationId);

        // Assert
        otherOrganization.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notMember.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notMemberRemove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LogoKeyAsync(otherOrganizationId)).Should().BeNull();
    }

    [Fact]
    public async Task InvitationPreview_ShouldShowTheOrganizationLogo()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        await UploadLogoAsync(owner, organizationId, Image(64, 64));
        var logoKey = await LogoKeyAsync(organizationId);
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = $"i{TestApi.Unique()}@example.com", role = OrgRole.Viewer });
        using var anonymous = _factory.CreateClient();

        // Act
        var (http, preview) = await anonymous.GetAsync<InvitationPreviewDto>($"organizations/invitation/preview?token={Uri.EscapeDataString(invitation.Data.Token)}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Data.OrganizationLogoUrl.Should().Contain(logoKey);
    }
}
