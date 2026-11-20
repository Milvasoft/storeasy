using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Apps.GetAppDetail;

/// <summary>
/// Handles <see cref="GetAppDetailQuery"/>. An app of another organization is reported as not found. Each build carries
/// the upload session whose package storeasy still keeps (<see cref="BuildPackages"/>).
/// </summary>
/// <param name="appRepository"></param>
/// <param name="sessionRepository"></param>
public class GetAppDetailQueryHandler(IStoreasyRepositoryBase<App> appRepository,
                                      IStoreasyRepositoryBase<ArtifactSession> sessionRepository) : IInterceptable, IQueryHandler<GetAppDetailQuery, AppDetailDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;

    /// <inheritdoc/>
    public async Task<Response<AppDetailDto>> Handle(GetAppDetailQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: AppDetailDto.Projection,
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<AppDetailDto>.Error(null, MessageKey.AppNotFound);

        await BuildPackages.FillAsync(_sessionRepository, request.OrganizationId, app.Builds, cancellationToken);

        return Response<AppDetailDto>.Success(app);
    }
}
