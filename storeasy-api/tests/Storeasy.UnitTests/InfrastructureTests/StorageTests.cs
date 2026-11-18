using FluentAssertions;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Storage;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Object storage rules and presigned URLs (no network).")]
public class StorageTests
{
    private static readonly StorageOptions _seaweedFs = new()
    {
        Bucket = "storeasy-local",
        Region = "eu-central-1",
        AccessKey = "test-access",
        SecretKey = "test-secret",
        ServiceUrl = "http://localhost:8333",
        ForcePathStyle = true,
    };

    [Theory]
    [InlineData(1, 1)]
    [InlineData(16L * 1024 * 1024, 1)]
    [InlineData(100L * 1024 * 1024, 7)]
    public void PartCount_ShouldUseSixteenMegabyteParts(long size, int expected)
    {
        AssetRules.PartCount(size).Should().Be(expected);
    }

    [Fact]
    public void PartSize_ShouldGrowSoThatTheFileFitsInTenThousandParts()
    {
        const long size = 400L * 1024 * 1024 * 1024;

        AssetRules.PartSize(size).Should().BeGreaterThan(AssetRules.DefaultPartSize);
        AssetRules.PartCount(size).Should().BeLessThanOrEqualTo(AssetRules.MaxPartCount);
    }

    [Theory]
    [InlineData(64L * 1024 * 1024, false)]
    [InlineData(64L * 1024 * 1024 + 1, true)]
    public void IsMultipart_ShouldStartAboveTheThreshold(long size, bool expected)
    {
        AssetRules.IsMultipart(size).Should().Be(expected);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("Ekran Görüntüsü 1.PNG", "Ekran-G-r-nt-s--1.PNG")]
    [InlineData("...", "file")]
    [InlineData(null, "file")]
    public void SafeFileName_ShouldKeepOnlySafeCharacters(string fileName, string expected)
    {
        AssetRules.SafeFileName(fileName).Should().Be(expected);
    }

    [Fact]
    public void ObjectKey_ShouldBeScopedToTheOrganizationAndPurpose()
    {
        var nonce = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        AssetRules.ObjectKey(42, AssetPurpose.BuildArtifact, "app-release.aab", nonce).Should().Be("org/42/buildartifact/0f8fad5bd9cb469fa16570867728950e/app-release.aab");
    }

    [Fact]
    public void MaxBytes_ShouldAllowLargeBuildsButNotLargeImages()
    {
        AssetRules.MaxBytes(AssetPurpose.BuildArtifact).Should().BeGreaterThan(AssetRules.MaxBytes(AssetPurpose.Screenshot));
    }

    [Fact]
    public void PresignedUrls_ShouldBeSignedForTheSeaweedFsEndpoint()
    {
        // Arrange
        using var client = S3FileStorage.CreateClient(_seaweedFs);
        var storage = new S3FileStorage(client, _seaweedFs);

        // Act
        var upload = storage.CreateUploadUrl("org/1/screenshot/a/home.png", "image/png", TimeSpan.FromMinutes(5));
        var part = storage.CreatePartUploadUrl("org/1/buildartifact/b/app.aab", "upload-1", 3, TimeSpan.FromMinutes(5));
        var download = storage.CreateDownloadUrl("org/1/screenshot/a/home.png", TimeSpan.FromMinutes(5), "home.png");

        // Assert
        storage.IsConfigured.Should().BeTrue();
        upload.Should().StartWith("http://localhost:8333/storeasy-local/org/1/screenshot/a/home.png?").And.Contain("X-Amz-Signature=");
        part.Should().Contain("uploadId=upload-1").And.Contain("partNumber=3");
        download.Should().Contain("response-content-disposition=");
        upload.Should().NotContain("test-secret");
    }

    [Fact]
    public void WithoutABucket_ShouldNotBeConfigured()
    {
        var options = new StorageOptions();

        S3FileStorage.CreateClient(options).Should().BeNull();
        new S3FileStorage(null, options).IsConfigured.Should().BeFalse();
    }
}
