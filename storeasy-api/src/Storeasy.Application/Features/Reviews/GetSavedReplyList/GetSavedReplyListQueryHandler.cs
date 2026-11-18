using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.GetSavedReplyList;

/// <summary>
/// Handles <see cref="GetSavedReplyListQuery"/>.
/// </summary>
/// <param name="SavedReplyRepository"></param>
public record GetSavedReplyListQueryHandler(IStoreasyRepositoryBase<SavedReply> SavedReplyRepository) : IInterceptable, IQueryHandler<GetSavedReplyListQuery, List<SavedReplyDto>>
{
    private readonly IStoreasyRepositoryBase<SavedReply> _savedReplyRepository = SavedReplyRepository;

    /// <inheritdoc/>
    public async Task<Response<List<SavedReplyDto>>> Handle(GetSavedReplyListQuery request, CancellationToken cancellationToken)
    {
        var replies = await _savedReplyRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId,
                                                              projection: SavedReplyDto.Projection,
                                                              cancellationToken: cancellationToken);

        var language = request.Locale?.Split('-')[0];

        var result = replies.Where(r => request.AppId == null || r.AppIds == null || r.AppIds.Count == 0 || r.AppIds.Contains(request.AppId.Value))
                            .OrderByDescending(r => language != null && r.Variants.Any(v => v.Locale != null && v.Locale.StartsWith(language, StringComparison.OrdinalIgnoreCase)))
                            .ThenByDescending(r => r.UseCount)
                            .ThenBy(r => r.Name)
                            .ToList();

        return Response<List<SavedReplyDto>>.Success(result);
    }
}
