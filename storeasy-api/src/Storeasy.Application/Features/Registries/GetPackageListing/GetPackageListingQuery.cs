using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetPackageListing;

/// <summary>
/// The package's page on its registry, read live (description, README as text, keywords, license, links, authors, icon),
/// with readiness checks. No registry offers an API that edits it: it ships with the next release, or comes from the
/// repository on Packagist.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPackageListingQuery : IQuery<PackageListingDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package.
    /// </summary>
    public int TrackedPackageId { get; set; }
}
