namespace Storeasy.Application.Interfaces;

/// <summary>
/// Redraws an uploaded organization logo (PNG, JPEG or WebP) as a square PNG of at most
/// <see cref="Storeasy.Domain.Organizations.OrganizationLogoRules.OutputSize"/> pixels: the centre square is kept and
/// scaled down, metadata and anything that is not pixels is dropped. CPU bound and thread safe; registered as a singleton.
/// </summary>
public interface ILogoImageNormalizer
{
    /// <summary>
    /// The redrawn PNG, or null when <paramref name="data"/> does not decode as an image or its size is outside the
    /// accepted range.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    byte[] Normalize(byte[] data);
}
