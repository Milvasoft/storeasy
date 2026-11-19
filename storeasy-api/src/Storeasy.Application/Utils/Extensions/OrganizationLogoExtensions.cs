using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// The URL a client shows an organization logo with.
/// </summary>
public static class OrganizationLogoExtensions
{
    /// <summary>
    /// A presigned GET URL of the logo stored under <paramref name="logoKey"/>, valid for
    /// <see cref="OrganizationLogoRules.UrlLifetime"/>; null when there is no logo or no storage. Creating it makes no
    /// network call.
    /// </summary>
    /// <param name="fileStorage"></param>
    /// <param name="logoKey"></param>
    /// <returns></returns>
    public static string LogoUrl(this IFileStorage fileStorage, string logoKey)
        => string.IsNullOrEmpty(logoKey) || !fileStorage.IsConfigured ? null : fileStorage.CreateDownloadUrl(logoKey, OrganizationLogoRules.UrlLifetime);
}
