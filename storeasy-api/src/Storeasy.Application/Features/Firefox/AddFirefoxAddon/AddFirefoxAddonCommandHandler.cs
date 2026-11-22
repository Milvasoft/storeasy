using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Firefox;
using Storeasy.Domain.Stores;
using System.Globalization;

namespace Storeasy.Application.Features.Firefox.AddFirefoxAddon;

/// <summary>
/// Handles <see cref="AddFirefoxAddonCommand"/>.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="AddonRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record AddFirefoxAddonCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                           IStoreasyRepositoryBase<App> AppRepository,
                                           IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                           IStoreasyRepositoryBase<FirefoxAddon> AddonRepository,
                                           IStoreJobQueue StoreJobQueue,
                                           IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<AddFirefoxAddonCommand, FirefoxAddonAddedDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<FirefoxAddon> _addonRepository = AddonRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<FirefoxAddonAddedDto>> Handle(AddFirefoxAddonCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<FirefoxAddonAddedDto>.Error(null, MessageKey.StoreConnectionNotFound);

        if (connection.Store != StoreKind.Firefox)
            return Response<FirefoxAddonAddedDto>.Error(null, MessageKey.StoreConnectionStoreMismatch);

        var reference = request.Addon.Trim();
        var guid = FirefoxVersions.IsGuid(reference) ? reference : null;

        var existing = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Firefox && (s.ExternalId == reference || (guid != null && s.BundleId == guid)),
                                                                        projection: s => (int?)s.Id,
                                                                        cancellationToken: cancellationToken);

        if (existing.HasValue)
            return Response<FirefoxAddonAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);

        App app = null;

        if (request.AppId is int appId)
        {
            app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, Name = a.Name },
                                                              cancellationToken: cancellationToken);

            if (app == null)
                return Response<FirefoxAddonAddedDto>.Error(null, MessageKey.AppNotFound);

            var linked = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == appId && s.Store == StoreKind.Firefox, projection: s => (int?)s.Id, cancellationToken: cancellationToken);

            if (linked.HasValue)
                return Response<FirefoxAddonAddedDto>.Error(null, MessageKey.AppStoreAlreadyLinked);
        }

        var name = app?.Name ?? (string.IsNullOrWhiteSpace(request.Name) ? reference : request.Name.Trim());
        var storeApp = new StoreApp
        {
            OrganizationId = request.OrganizationId,
            StoreConnectionId = connection.Id,
            Store = StoreKind.Firefox,
            ExternalId = reference,
            BundleId = guid,
            Name = name,
        };

        if (app == null)
            storeApp.App = new App { OrganizationId = request.OrganizationId, Name = name };
        else
            storeApp.AppId = app.Id;

        var addonId = long.TryParse(reference, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric) ? numeric : 0;

        try
        {
            // One insert for the app (when new), the record and the add-on row.
            await _addonRepository.AddAsync(new FirefoxAddon { OrganizationId = request.OrganizationId, AddonId = addonId, Guid = guid, StoreApp = storeApp }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Response<FirefoxAddonAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.FirefoxSync, new FirefoxSyncPayload(connection.Id, storeApp.Id), StoreKind.Firefox, storeApp.AppId)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.FirefoxAddonAdded, storeApp.Id.ToString(CultureInfo.InvariantCulture), $"Firefox add-on {reference} added.", StoreKind.Firefox, cancellationToken: cancellationToken);

        return Response<FirefoxAddonAddedDto>.Success(new FirefoxAddonAddedDto { AppId = storeApp.AppId, StoreAppId = storeApp.Id, JobId = jobIds[0] });
    }
}
