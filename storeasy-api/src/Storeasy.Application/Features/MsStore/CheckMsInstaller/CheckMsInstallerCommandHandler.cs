using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.CheckMsInstaller;

/// <summary>
/// Handles <see cref="CheckMsInstallerCommand"/>: only MSI/EXE products have installers.
/// </summary>
/// <param name="ProductRepository"></param>
/// <param name="StoreJobQueue"></param>
[Log]
public record CheckMsInstallerCommandHandler(IStoreasyRepositoryBase<MsProduct> ProductRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<CheckMsInstallerCommand, long>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = ProductRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(CheckMsInstallerCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetMsProductAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (product == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (product.Kind != MsProductKind.MsiExe)
            return Response<long>.Error(0, MessageKey.MsWrongProductKind);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId,
                                                                            StoreJobKind.MsCheckInstaller,
                                                                            new MsCheckInstallerPayload(product.StoreAppId, request.PackageUrls ?? []),
                                                                            StoreKind.Microsoft,
                                                                            product.AppId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
