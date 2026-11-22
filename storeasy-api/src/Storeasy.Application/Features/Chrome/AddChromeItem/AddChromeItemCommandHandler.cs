using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.Chrome.AddChromeItem;

/// <summary>
/// Handles <see cref="AddChromeItemCommand"/>.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="ItemRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record AddChromeItemCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                         IStoreasyRepositoryBase<App> AppRepository,
                                         IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                         IStoreasyRepositoryBase<ChromeItem> ItemRepository,
                                         IStoreJobQueue StoreJobQueue,
                                         IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<AddChromeItemCommand, ChromeItemAddedDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<ChromeItem> _itemRepository = ItemRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<ChromeItemAddedDto>> Handle(AddChromeItemCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<ChromeItemAddedDto>.Error(null, MessageKey.StoreConnectionNotFound);

        if (connection.Store != StoreKind.Chrome)
            return Response<ChromeItemAddedDto>.Error(null, MessageKey.StoreConnectionStoreMismatch);

        var itemId = request.ItemId.Trim();

        var existing = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Chrome && s.ExternalId == itemId,
                                                                        projection: s => (int?)s.Id,
                                                                        cancellationToken: cancellationToken);

        if (existing.HasValue)
            return Response<ChromeItemAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);

        App app = null;

        if (request.AppId is int appId)
        {
            app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, Name = a.Name },
                                                              cancellationToken: cancellationToken);

            if (app == null)
                return Response<ChromeItemAddedDto>.Error(null, MessageKey.AppNotFound);

            var linked = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == appId && s.Store == StoreKind.Chrome, projection: s => (int?)s.Id, cancellationToken: cancellationToken);

            if (linked.HasValue)
                return Response<ChromeItemAddedDto>.Error(null, MessageKey.AppStoreAlreadyLinked);
        }

        var name = app?.Name ?? (string.IsNullOrWhiteSpace(request.Name) ? itemId : request.Name.Trim());
        var storeApp = new StoreApp
        {
            OrganizationId = request.OrganizationId,
            StoreConnectionId = connection.Id,
            Store = StoreKind.Chrome,
            ExternalId = itemId,
            BundleId = itemId,
            Name = name,
        };

        if (app == null)
            storeApp.App = new App { OrganizationId = request.OrganizationId, Name = name };
        else
            storeApp.AppId = app.Id;

        try
        {
            // One insert for the app (when new), the record and the item row.
            await _itemRepository.AddAsync(new ChromeItem { OrganizationId = request.OrganizationId, ItemId = itemId, StoreApp = storeApp }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Response<ChromeItemAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.ChromeSync, new ChromeSyncPayload(connection.Id, storeApp.Id), StoreKind.Chrome, storeApp.AppId)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.ChromeItemAdded, storeApp.Id.ToString(), $"Chrome Web Store item {itemId} added.", StoreKind.Chrome, cancellationToken: cancellationToken);

        return Response<ChromeItemAddedDto>.Success(new ChromeItemAddedDto { AppId = storeApp.AppId, StoreAppId = storeApp.Id, JobId = jobIds[0] });
    }
}
