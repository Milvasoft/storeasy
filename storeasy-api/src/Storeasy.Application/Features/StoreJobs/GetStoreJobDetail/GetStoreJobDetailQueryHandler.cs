using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreJobs.GetStoreJobDetail;

/// <summary>
/// Handles <see cref="GetStoreJobDetailQuery"/>. A job of another organization is reported as not found.
/// </summary>
/// <param name="jobRepository"></param>
public class GetStoreJobDetailQueryHandler(IStoreasyRepositoryBase<StoreJob> jobRepository) : IInterceptable, IQueryHandler<GetStoreJobDetailQuery, StoreJobDetailDto>
{
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;

    /// <inheritdoc/>
    public async Task<Response<StoreJobDetailDto>> Handle(GetStoreJobDetailQuery request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetFirstOrDefaultAsync(j => j.Id == request.JobId && j.OrganizationId == request.OrganizationId,
                                                              projection: StoreJobDetailDto.Projection,
                                                              cancellationToken: cancellationToken);

        if (job == null)
            return Response<StoreJobDetailDto>.Error(null, MessageKey.StoreJobNotFound);

        return Response<StoreJobDetailDto>.Success(job);
    }
}
