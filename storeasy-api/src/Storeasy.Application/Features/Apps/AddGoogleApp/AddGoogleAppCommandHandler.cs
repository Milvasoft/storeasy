using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.Apps.AddGoogleApp;

/// <summary>
/// Handles <see cref="AddGoogleAppCommand"/>. The record and its app are written first (unconfirmed:
/// <see cref="StoreApp.SyncedDate"/> is null), then the sync job of the connection is queued.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record AddGoogleAppCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                         IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                         IStoreasyRepositoryBase<App> AppRepository,
                                         IStoreJobQueue StoreJobQueue,
                                         IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<AddGoogleAppCommand, AppAddedDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<AppAddedDto>> Handle(AddGoogleAppCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<AppAddedDto>.Error(null, MessageKey.StoreConnectionNotFound);

        if (connection.Store != StoreKind.Google)
            return Response<AppAddedDto>.Error(null, MessageKey.StoreConnectionStoreMismatch);

        var packageName = request.PackageName.Trim();
        var linkKey = AppRules.LinkKey(packageName);

        var existing = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Google && s.ExternalId == packageName,
                                                                        projection: s => (int?)s.Id,
                                                                        cancellationToken: cancellationToken);

        if (existing.HasValue)
            return Response<AppAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);

        // The same product in another store: same platform identifier and no Play record yet.
        var appId = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId
                                                                           && s.Store != StoreKind.Google
                                                                           && s.BundleId.ToLower() == linkKey
                                                                           && !s.App.IsDeleted
                                                                           && !s.App.StoreApps.Any(o => o.Store == StoreKind.Google),
                                                                      projection: s => (int?)s.AppId,
                                                                      cancellationToken: cancellationToken);

        var storeApp = new StoreApp
        {
            OrganizationId = request.OrganizationId,
            StoreConnectionId = connection.Id,
            Store = StoreKind.Google,
            ExternalId = packageName,
            BundleId = packageName,
            Name = packageName,
        };

        try
        {
            if (appId.HasValue)
            {
                storeApp.AppId = appId.Value;

                await _storeAppRepository.AddAsync(storeApp, cancellationToken);
            }
            else
            {
                var app = new App { OrganizationId = request.OrganizationId, Name = packageName, StoreApps = [storeApp] };

                await _appRepository.AddAsync(app, cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            // A concurrent add of the same package (or of a Play record to the same app) won the unique index.
            return Response<AppAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncApps, new SyncAppsPayload(connection.Id), StoreKind.Google, storeApp.AppId)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.AppAdded, storeApp.Id.ToString(), $"Play app {packageName} added.", StoreKind.Google, cancellationToken: cancellationToken);

        return Response<AppAddedDto>.Success(new AppAddedDto { AppId = storeApp.AppId, StoreAppId = storeApp.Id, JobId = jobIds[0] });
    }
}
