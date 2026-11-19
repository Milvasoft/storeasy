using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.PreReviewDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.PreReviews.GetPreReviewList;

/// <summary>
/// Handles <see cref="GetPreReviewListQuery"/>. An app of another organization gives an empty list.
/// </summary>
/// <param name="PreReviewRepository"></param>
/// <param name="JobRepository"></param>
public record GetPreReviewListQueryHandler(IStoreasyRepositoryBase<PreReview> PreReviewRepository,
                                           IStoreasyRepositoryBase<StoreJob> JobRepository) : IInterceptable, IQueryHandler<GetPreReviewListQuery, List<PreReviewListDto>>
{
    private readonly IStoreasyRepositoryBase<PreReview> _preReviewRepository = PreReviewRepository;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = JobRepository;

    /// <inheritdoc/>
    public async Task<Response<List<PreReviewListDto>>> Handle(GetPreReviewListQuery request, CancellationToken cancellationToken)
    {
        var page = await _preReviewRepository.GetAllAsync(new ListRequest
                                                          {
                                                              PageNumber = 1,
                                                              RowCount = GetPreReviewListQuery.MaxRows,
                                                              Sorting = new SortRequest { SortBy = nameof(PreReview.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc },
                                                          },
                                                          condition: r => r.OrganizationId == request.OrganizationId && r.AppId == request.AppId && r.Store == request.Store,
                                                          projection: PreReviewRepositoryExtensions.Projection,
                                                          cancellationToken: cancellationToken);

        var rows = page.Data ?? [];

        await _jobRepository.ResolveInterruptedAsync(rows, cancellationToken);

        return Response<List<PreReviewListDto>>.Success([.. rows.Select(r => r.ToListDto())]);
    }
}
