using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Events;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Apps.GetAppDetail;

/// <summary>
/// Handles <see cref="GetAppDetailQuery"/>. An app of another organization is reported as not found. Each build carries
/// the upload session whose package storeasy still keeps (<see cref="BuildPackages"/>), and each store record the state of
/// its latest known store version (<see cref="AppVersionState"/>).
/// </summary>
/// <param name="appRepository"></param>
/// <param name="sessionRepository"></param>
/// <param name="versionStateRepository"></param>
public class GetAppDetailQueryHandler(IStoreasyRepositoryBase<App> appRepository,
                                      IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                      IStoreasyRepositoryBase<AppVersionState> versionStateRepository) : IInterceptable, IQueryHandler<GetAppDetailQuery, AppDetailDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IStoreasyRepositoryBase<AppVersionState> _versionStateRepository = versionStateRepository;

    /// <inheritdoc/>
    public async Task<Response<AppDetailDto>> Handle(GetAppDetailQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: AppDetailDto.Projection,
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<AppDetailDto>.Error(null, MessageKey.AppNotFound);

        await BuildPackages.FillAsync(_sessionRepository, request.OrganizationId, app.Builds, cancellationToken);
        await FillVersionStatesAsync(request.OrganizationId, app, cancellationToken);

        return Response<AppDetailDto>.Success(app);
    }

    /// <summary>
    /// The latest changed version state of each store record.
    /// </summary>
    private async Task FillVersionStatesAsync(int organizationId, AppDetailDto app, CancellationToken cancellationToken)
    {
        var records = app.Stores ?? [];

        if (records.Count == 0)
            return;

        var states = await _versionStateRepository.GetAllAsync(s => s.OrganizationId == organizationId && s.AppId == app.Id,
                                                               projection: s => new AppVersionState { StoreAppId = s.StoreAppId, State = s.State, VersionString = s.VersionString, BuildNumber = s.BuildNumber, ChangedDate = s.ChangedDate },
                                                               cancellationToken: cancellationToken);

        var latest = states.GroupBy(s => s.StoreAppId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.ChangedDate).First());

        foreach (var record in records)
        {
            if (!latest.TryGetValue(record.StoreAppId, out var state))
                continue;

            record.VersionState = state.State;
            record.VersionString = state.VersionString;
            record.VersionBuildNumber = state.BuildNumber;
            record.VersionStateDate = state.ChangedDate;
        }
    }
}
