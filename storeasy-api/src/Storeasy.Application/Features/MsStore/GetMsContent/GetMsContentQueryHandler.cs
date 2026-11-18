using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.MsStore.GetMsContent;

/// <summary>
/// Handles <see cref="GetMsContentQuery"/>. The connection's secret is opened only for the reads of this request; a store
/// failure is answered with the store's redacted message.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="microsoftStoreClient"></param>
public class GetMsContentQueryHandler(IStoreasyRepositoryBase<MsProduct> productRepository,
                                      IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                      ISecretProtector secretProtector,
                                      IMicrosoftStoreClient microsoftStoreClient) : IInterceptable, IQueryHandler<GetMsContentQuery, MsContentDto>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;

    /// <inheritdoc/>
    public async Task<Response<MsContentDto>> Handle(GetMsContentQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetMsProductAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (product == null)
            return Response<MsContentDto>.Error(null, MessageKey.StoreAppNotFound);

        var secret = await _connectionRepository.OpenMsSecretAsync(_secretProtector, request.OrganizationId, product.ConnectionId, cancellationToken);

        if (secret == null)
            return Response<MsContentDto>.Error(null, MessageKey.StoreConnectionNotFound);

        var target = product.Kind == MsProductKind.MsiExe
            ? MsSubmissionTarget.Installer
            : !string.IsNullOrEmpty(request.FlightId) ? MsSubmissionTarget.Flight : !string.IsNullOrEmpty(request.AddonStoreId) ? MsSubmissionTarget.Addon : MsSubmissionTarget.App;

        try
        {
            var content = await MsContentReader.ReadAsync(_microsoftStoreClient,
                                                          secret,
                                                          new MsContentReader.Request { Kind = product.Kind, ProductId = product.ProductId, Target = target, FlightId = request.FlightId, AddonStoreId = request.AddonStoreId },
                                                          cancellationToken);

            return Response<MsContentDto>.Success(content);
        }
        catch (StoreCallException ex)
        {
            return MsResponses.StoreFailure<MsContentDto>(ex);
        }
    }
}
