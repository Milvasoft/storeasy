using FluentAssertions;
using SkiaSharp;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.Infrastructure.Services;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Organization logo rules and the logo redraw.")]
public class OrganizationLogoTests
{
    private static readonly SkiaLogoImageNormalizer _normalizer = new();

    /// <summary>
    /// An image whose left half is red and right half blue, with a green square in the middle.
    /// </summary>
    private static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = false };

        paint.Color = SKColors.Red;
        canvas.DrawRect(0, 0, width / 2f, height, paint);
        paint.Color = SKColors.Blue;
        canvas.DrawRect(width / 2f, 0, width / 2f, height, paint);
        paint.Color = SKColors.Lime;
        canvas.DrawRect((width / 2f) - 4, (height / 2f) - 4, 8, 8, paint);
        canvas.Flush();

        using var data = bitmap.Encode(format, 90);

        return data.ToArray();
    }

    private static SKBitmap Decode(byte[] png) => SKBitmap.Decode(png);

    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public void DetectContentType_ShouldReadTheSignature(SKEncodedImageFormat format, string expected)
    {
        OrganizationLogoRules.DetectContentType(Image(32, 32, format)).Should().Be(expected);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("GIF89a......")]
    [InlineData("")]
    [InlineData("RIFF\0\0\0\0WAVE")]
    public void DetectContentType_ShouldRefuseOtherFiles(string content)
    {
        OrganizationLogoRules.DetectContentType(System.Text.Encoding.ASCII.GetBytes(content)).Should().BeNull();
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData(" IMAGE/JPEG ", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/svg+xml", false)]
    [InlineData("image/gif", false)]
    [InlineData(null, false)]
    public void IsAcceptedContentType_ShouldAllowPngJpegAndWebpOnly(string contentType, bool expected)
    {
        OrganizationLogoRules.IsAcceptedContentType(contentType).Should().Be(expected);
    }

    [Fact]
    public void ObjectKey_ShouldBeScopedToTheOrganizationAndFreshPerLogo()
    {
        var first = OrganizationLogoRules.ObjectKey(7, Guid.NewGuid());
        var second = OrganizationLogoRules.ObjectKey(7, Guid.NewGuid());

        first.Should().StartWith("org/7/logo/").And.EndWith(".png");
        first.Should().NotBe(second);
    }

    [Fact]
    public void MaxBytes_OfALogoUpload_ShouldBeTwoMegabytes()
    {
        AssetRules.MaxBytes(AssetPurpose.OrganizationLogo).Should().Be(2L * 1024 * 1024);
    }

    [Fact]
    public void Normalize_ShouldCropTheCentreSquareAndScaleItDown()
    {
        var png = _normalizer.Normalize(Image(1000, 600, SKEncodedImageFormat.Png));

        using var bitmap = Decode(png);

        OrganizationLogoRules.DetectContentType(png).Should().Be("image/png");
        bitmap.Width.Should().Be(OrganizationLogoRules.OutputSize);
        bitmap.Height.Should().Be(OrganizationLogoRules.OutputSize);
        bitmap.GetPixel(10, 128).Red.Should().BeGreaterThan(200, "the left of the centre square is red");
        bitmap.GetPixel(245, 128).Blue.Should().BeGreaterThan(200, "the right of the centre square is blue");
    }

    [Fact]
    public void Normalize_ShouldKeepASmallSquareAtItsSize()
    {
        var png = _normalizer.Normalize(Image(64, 64, SKEncodedImageFormat.Jpeg));

        using var bitmap = Decode(png);

        bitmap.Width.Should().Be(64);
        bitmap.Height.Should().Be(64);
    }

    [Fact]
    public void Normalize_ShouldAcceptWebp()
    {
        _normalizer.Normalize(Image(300, 300, SKEncodedImageFormat.Webp)).Should().NotBeNull();
    }

    [Fact]
    public void Normalize_ShouldRefuseBytesThatAreNotAnImage()
    {
        var broken = Image(64, 64, SKEncodedImageFormat.Png)[..40];

        _normalizer.Normalize(broken).Should().BeNull();
        _normalizer.Normalize(System.Text.Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")).Should().BeNull();
        _normalizer.Normalize([]).Should().BeNull();
    }

    [Fact]
    public void Normalize_ShouldRefuseATinyImage()
    {
        _normalizer.Normalize(Image(8, 8, SKEncodedImageFormat.Png)).Should().BeNull();
    }
}
