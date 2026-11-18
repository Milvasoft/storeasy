using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.GetMsSubmissionList;

/// <summary>
/// Handles <see cref="GetMsSubmissionListQuery"/>. A record of another organization or another store is reported as not found.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="submissionRepository"></param>
public class GetMsSubmissionListQueryHandler(IStoreasyRepositoryBase<MsProduct> productRepository, IStoreasyRepositoryBase<MsSubmission> submissionRepository) : IInterceptable, IQueryHandler<GetMsSubmissionListQuery, List<MsSubmissionDto>>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = submissionRepository;

    /// <inheritdoc/>
    public async Task<Response<List<MsSubmissionDto>>> Handle(GetMsSubmissionListQuery request, CancellationToken cancellationToken)
    {
        if (!await _productRepository.MsProductExistsAsync(request.OrganizationId, request.StoreAppId, cancellationToken))
            return Response<List<MsSubmissionDto>>.Error(null, MessageKey.StoreAppNotFound);

        var items = await _submissionRepository.GetAllAsync(x => x.StoreAppId == request.StoreAppId && x.OrganizationId == request.OrganizationId,
                                                            projection: MsSubmissionDto.Projection,
                                                            cancellationToken: cancellationToken);

        return Response<List<MsSubmissionDto>>.Success([.. items.OrderByDescending(s => s.Id)]);
    }
}
