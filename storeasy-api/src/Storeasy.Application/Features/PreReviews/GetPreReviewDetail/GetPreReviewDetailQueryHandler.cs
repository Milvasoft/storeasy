using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.PreReviewDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.PreReviews.GetPreReviewDetail;

/// <summary>
/// Handles <see cref="GetPreReviewDetailQuery"/>. A pre-review of another organization is not found.
/// </summary>
/// <param name="PreReviewRepository"></param>
/// <param name="JobRepository"></param>
/// <param name="Localizer"></param>
public record GetPreReviewDetailQueryHandler(IStoreasyRepositoryBase<PreReview> PreReviewRepository,
                                             IStoreasyRepositoryBase<StoreJob> JobRepository,
                                             IMilvaLocalizer Localizer) : IInterceptable, IQueryHandler<GetPreReviewDetailQuery, PreReviewDto>
{
    private readonly IStoreasyRepositoryBase<PreReview> _preReviewRepository = PreReviewRepository;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = JobRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<PreReviewDto>> Handle(GetPreReviewDetailQuery request, CancellationToken cancellationToken)
    {
        var row = await _preReviewRepository.GetFirstOrDefaultAsync(r => r.Id == request.Id && r.OrganizationId == request.OrganizationId,
                                                                    projection: PreReviewRepositoryExtensions.Projection,
                                                                    cancellationToken: cancellationToken);

        if (row == null)
            return Response<PreReviewDto>.Error(null, MessageKey.PreReviewNotFound);

        await _jobRepository.ResolveInterruptedAsync([row], cancellationToken);

        return Response<PreReviewDto>.Success(row.ToDto(_localizer));
    }
}
