using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.GetMsInstallerPackageList;

/// <summary>
/// Handles <see cref="GetMsInstallerPackageListQuery"/>. A record of another organization or another store is reported as not found.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="packageRepository"></param>
public class GetMsInstallerPackageListQueryHandler(IStoreasyRepositoryBase<MsProduct> productRepository, IStoreasyRepositoryBase<MsInstallerPackage> packageRepository) : IInterceptable, IQueryHandler<GetMsInstallerPackageListQuery, List<MsInstallerPackageDto>>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _packageRepository = packageRepository;

    /// <inheritdoc/>
    public async Task<Response<List<MsInstallerPackageDto>>> Handle(GetMsInstallerPackageListQuery request, CancellationToken cancellationToken)
    {
        if (!await _productRepository.MsProductExistsAsync(request.OrganizationId, request.StoreAppId, cancellationToken))
            return Response<List<MsInstallerPackageDto>>.Error(null, MessageKey.StoreAppNotFound);

        var items = await _packageRepository.GetAllAsync(x => x.StoreAppId == request.StoreAppId && x.OrganizationId == request.OrganizationId,
                                                         projection: MsInstallerPackageDto.Projection,
                                                         cancellationToken: cancellationToken);

        return Response<List<MsInstallerPackageDto>>.Success([.. items.OrderBy(p => p.PackageUrl)]);
    }
}
