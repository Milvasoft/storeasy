using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;
using Storeasy.Domain.Winget;

namespace Storeasy.Application.Features.Winget.AddWingetPackage;

/// <summary>
/// Handles <see cref="AddWingetPackageCommand"/>.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="PackageRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record AddWingetPackageCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                            IStoreasyRepositoryBase<App> AppRepository,
                                            IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                            IStoreasyRepositoryBase<WingetPackage> PackageRepository,
                                            IStoreJobQueue StoreJobQueue,
                                            IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<AddWingetPackageCommand, WingetPackageAddedDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<WingetPackage> _packageRepository = PackageRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<WingetPackageAddedDto>> Handle(AddWingetPackageCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<WingetPackageAddedDto>.Error(null, MessageKey.StoreConnectionNotFound);

        if (connection.Store != StoreKind.Winget)
            return Response<WingetPackageAddedDto>.Error(null, MessageKey.StoreConnectionStoreMismatch);

        var identifier = request.PackageIdentifier.Trim();

        var existing = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Winget && s.ExternalId == identifier,
                                                                        projection: s => (int?)s.Id,
                                                                        cancellationToken: cancellationToken);

        if (existing.HasValue)
            return Response<WingetPackageAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);

        App app;

        if (request.AppId is int appId)
        {
            app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, Name = a.Name },
                                                              cancellationToken: cancellationToken);

            if (app == null)
                return Response<WingetPackageAddedDto>.Error(null, MessageKey.AppNotFound);

            var linked = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == appId && s.Store == StoreKind.Winget, projection: s => (int?)s.Id, cancellationToken: cancellationToken);

            if (linked.HasValue)
                return Response<WingetPackageAddedDto>.Error(null, MessageKey.AppStoreAlreadyLinked);
        }
        else
        {
            app = null;
        }

        var name = app?.Name ?? (string.IsNullOrWhiteSpace(request.Name) ? identifier : request.Name.Trim());
        var storeApp = new StoreApp
        {
            OrganizationId = request.OrganizationId,
            StoreConnectionId = connection.Id,
            Store = StoreKind.Winget,
            ExternalId = identifier,
            BundleId = identifier,
            Name = name,
        };

        if (app == null)
            storeApp.App = new App { OrganizationId = request.OrganizationId, Name = name };
        else
            storeApp.AppId = app.Id;

        try
        {
            // One insert for the app (when new), the record and the package row.
            await _packageRepository.AddAsync(new WingetPackage { OrganizationId = request.OrganizationId, PackageIdentifier = identifier, StoreApp = storeApp }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent add of the same package won the unique index.
            return Response<WingetPackageAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.WingetSync, new WingetSyncPayload(connection.Id, storeApp.Id), StoreKind.Winget, storeApp.AppId)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.WingetPackageAdded, storeApp.Id.ToString(), $"winget package {identifier} added.", StoreKind.Winget, cancellationToken: cancellationToken);

        return Response<WingetPackageAddedDto>.Success(new WingetPackageAddedDto { AppId = storeApp.AppId, StoreAppId = storeApp.Id, JobId = jobIds[0] });
    }
}
