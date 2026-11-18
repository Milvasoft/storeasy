using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ReadinessDtos;

namespace Storeasy.Application.Features.Readiness.GetReadiness;

/// <summary>
/// Handles <see cref="GetReadinessQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
public class GetReadinessQueryHandler(IStoreContentService contentService,
                                      IReadinessService readinessService) : IInterceptable, IQueryHandler<GetReadinessQuery, ReadinessReportDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;

    /// <inheritdoc/>
    public async Task<Response<ReadinessReportDto>> Handle(GetReadinessQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<ReadinessReportDto>.Error(null, MessageKey.StoreAppNotFound);

        return await _readinessService.CheckAsync(app, cancellationToken);
    }
}
