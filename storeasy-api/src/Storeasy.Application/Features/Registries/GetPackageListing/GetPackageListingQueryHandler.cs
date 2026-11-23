using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetPackageListing;

/// <summary>
/// Handles <see cref="GetPackageListingQuery"/>. A package of another organization is reported as not found; a registry
/// that cannot be read answers the error on the result instead of failing.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="registryReader"></param>
public class GetPackageListingQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository, IPackageRegistryReader registryReader) : IInterceptable, IQueryHandler<GetPackageListingQuery, PackageListingDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;
    private readonly IPackageRegistryReader _registryReader = registryReader;

    /// <inheritdoc/>
    public async Task<Response<PackageListingDto>> Handle(GetPackageListingQuery request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == request.TrackedPackageId && p.OrganizationId == request.OrganizationId,
                                                                      projection: p => new TrackedPackage { Id = p.Id, Registry = p.Registry, Name = p.Name, LatestVersion = p.LatestVersion },
                                                                      cancellationToken: cancellationToken);

        if (package == null)
            return Response<PackageListingDto>.Error(null, MessageKey.TrackedPackageNotFound);

        var dto = new PackageListingDto
        {
            TrackedPackageId = package.Id,
            Registry = package.Registry,
            Available = package.Registry != PackageRegistry.Maven,
            ShipsWithRelease = RegistryListing.ShipsWithRelease(package.Registry),
            RegistryUrl = RegistryNames.PageUrl(package.Registry, package.Name),
        };

        if (!dto.Available)
            return Response<PackageListingDto>.Success(dto);

        RegistryListingRead listing;

        try
        {
            listing = await _registryReader.ReadListingAsync(package.Registry, package.Name, package.LatestVersion, cancellationToken);
        }
        catch (StoreCallException ex)
        {
            dto.ReadError = ex.Message;
            return Response<PackageListingDto>.Success(dto);
        }

        if (listing == null)
            return Response<PackageListingDto>.Success(dto);

        dto.Version = listing.Version ?? package.LatestVersion;
        dto.Description = listing.Description;
        dto.Readme = listing.Readme;
        dto.ReadmeFormat = listing.ReadmeFormat;
        dto.Keywords = listing.Keywords;
        dto.License = listing.License;
        dto.Homepage = listing.Homepage;
        dto.Repository = listing.Repository;
        dto.Authors = listing.Authors;
        dto.IconUrl = listing.IconUrl;
        dto.Checks = [.. RegistryListing.Checks(package.Registry, listing).Select(c => new ListingCheckDto { Key = c.Key, Status = c.Status })];

        return Response<PackageListingDto>.Success(dto);
    }
}
