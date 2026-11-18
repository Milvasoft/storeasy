using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.MsStore.AddMsProduct;

/// <summary>
/// Handles <see cref="AddMsProductCommand"/>. The record, its app and its product row are written in one insert (the
/// record is unconfirmed: <see cref="MsProduct.SyncedDate"/> is null), then the connection's sync job is queued.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="ProductRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record AddMsProductCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                         IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                         IStoreasyRepositoryBase<MsProduct> ProductRepository,
                                         IStoreJobQueue StoreJobQueue,
                                         IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<AddMsProductCommand, MsProductAddedDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = ProductRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<MsProductAddedDto>> Handle(AddMsProductCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<MsProductAddedDto>.Error(null, MessageKey.StoreConnectionNotFound);

        if (connection.Store != StoreKind.Microsoft)
            return Response<MsProductAddedDto>.Error(null, MessageKey.StoreConnectionStoreMismatch);

        var productId = request.ProductId.Trim().ToUpperInvariant();

        var existing = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Microsoft && s.ExternalId == productId,
                                                                        projection: s => (int?)s.Id,
                                                                        cancellationToken: cancellationToken);

        if (existing.HasValue)
            return Response<MsProductAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);

        var name = string.IsNullOrWhiteSpace(request.Name) ? productId : request.Name.Trim();
        var app = new App { OrganizationId = request.OrganizationId, Name = name };
        var storeApp = new StoreApp
        {
            OrganizationId = request.OrganizationId,
            StoreConnectionId = connection.Id,
            Store = StoreKind.Microsoft,
            ExternalId = productId,
            Name = name,
            App = app,
        };

        try
        {
            // One insert for the app, its record and the product row.
            await _productRepository.AddAsync(new MsProduct { OrganizationId = request.OrganizationId, Kind = MsProductKind.MsiExe, StoreApp = storeApp }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent add of the same product won the unique index.
            return Response<MsProductAddedDto>.Error(null, MessageKey.StoreAppAlreadyAdded);
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.MsSync, new MsSyncPayload(connection.Id), StoreKind.Microsoft, app.Id)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.MsProductAdded, storeApp.Id.ToString(), $"Microsoft Store product {productId} added.", StoreKind.Microsoft, cancellationToken: cancellationToken);

        return Response<MsProductAddedDto>.Success(new MsProductAddedDto { AppId = app.Id, StoreAppId = storeApp.Id, JobId = jobIds[0] });
    }
}
