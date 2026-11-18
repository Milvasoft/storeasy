using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.LinkStoreApp;

/// <summary>
/// Handles <see cref="LinkStoreAppCommand"/>. The move and the removal of the emptied app commit together.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="TransactionRunner"></param>
/// <param name="AuditTrail"></param>
[Log]
public record LinkStoreAppCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                         IStoreasyRepositoryBase<App> AppRepository,
                                         ITransactionRunner TransactionRunner,
                                         IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<LinkStoreAppCommand, int>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(LinkStoreAppCommand request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId,
                                                                        projection: s => new { s.Id, s.AppId, s.Store, s.ExternalId },
                                                                        cancellationToken: cancellationToken);

        if (storeApp == null)
            return Response<int>.Error(0, MessageKey.StoreAppNotFound);

        var target = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                                 projection: a => new { a.Id, Stores = a.StoreApps.Select(s => s.Store).ToList() },
                                                                 cancellationToken: cancellationToken);

        if (target == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        if (storeApp.AppId == target.Id)
            return Response<int>.Success(target.Id);

        if (target.Stores.Contains(storeApp.Store))
            return Response<int>.Error(0, MessageKey.AppStoreAlreadyLinked);

        var previousAppId = storeApp.AppId;

        await _transactionRunner.RunAsync(async () =>
        {
            await _storeAppRepository.ExecuteUpdateAsync(storeApp.Id, new SetPropertyBuilder<StoreApp>().SetPropertyValue(s => s.AppId, target.Id), cancellationToken);

            await _appRepository.ExecuteDeleteAsync(a => a.Id == previousAppId && !a.StoreApps.Any(), cancellationToken: cancellationToken);
        });

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.StoreAppLinked,
                                     storeApp.Id.ToString(),
                                     $"{storeApp.Store} record {storeApp.ExternalId} moved from app {previousAppId} to app {target.Id}.",
                                     storeApp.Store,
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(target.Id);
    }
}
