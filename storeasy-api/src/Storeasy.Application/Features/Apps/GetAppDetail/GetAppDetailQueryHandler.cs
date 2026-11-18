using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.GetAppDetail;

/// <summary>
/// Handles <see cref="GetAppDetailQuery"/>. An app of another organization is reported as not found.
/// </summary>
/// <param name="appRepository"></param>
public class GetAppDetailQueryHandler(IStoreasyRepositoryBase<App> appRepository) : IInterceptable, IQueryHandler<GetAppDetailQuery, AppDetailDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;

    /// <inheritdoc/>
    public async Task<Response<AppDetailDto>> Handle(GetAppDetailQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: AppDetailDto.Projection,
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<AppDetailDto>.Error(null, MessageKey.AppNotFound);

        return Response<AppDetailDto>.Success(app);
    }
}
