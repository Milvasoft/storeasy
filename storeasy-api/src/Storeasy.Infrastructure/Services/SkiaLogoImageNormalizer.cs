using SkiaSharp;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Organizations;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// <see cref="ILogoImageNormalizer"/> with SkiaSharp (the library of the mockup renderer). The header is read first, so an
/// image whose size is outside <see cref="OrganizationLogoRules.MinSourceDimension"/> and
/// <see cref="OrganizationLogoRules.MaxSourceDimension"/> is refused before it is decoded. The centre square is drawn on a
/// transparent canvas of at most <see cref="OrganizationLogoRules.OutputSize"/> pixels and written as PNG, so the stored
/// file holds only pixels.
/// </summary>
public sealed class SkiaLogoImageNormalizer : ILogoImageNormalizer
{
    private static readonly SKSamplingOptions _sampling = new(SKCubicResampler.Mitchell);

    /// <inheritdoc/>
    public byte[] Normalize(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;

        using var encoded = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(encoded);

        if (codec == null)
            return null;

        var info = codec.Info;

        if (info.Width < OrganizationLogoRules.MinSourceDimension
            || info.Height < OrganizationLogoRules.MinSourceDimension
            || info.Width > OrganizationLogoRules.MaxSourceDimension
            || info.Height > OrganizationLogoRules.MaxSourceDimension)
            return null;

        // A truncated or corrupt file decodes only partly; anything but a complete decode is refused.
        var decodeInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);

        if (codec.GetPixels(decodeInfo, decoded.GetPixels()) != SKCodecResult.Success)
            return null;

        using var image = SKImage.FromBitmap(decoded);

        if (image == null)
            return null;

        var side = Math.Min(image.Width, image.Height);
        var size = Math.Min(side, OrganizationLogoRules.OutputSize);
        var source = SKRect.Create((image.Width - side) / 2f, (image.Height - side) / 2f, side, side);

        using var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(image, source, SKRect.Create(0, 0, size, size), _sampling);
        canvas.Flush();

        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return png?.ToArray();
    }
}
