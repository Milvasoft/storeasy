using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.UnlinkStoreApp;

/// <summary>
/// Handles <see cref="UnlinkStoreAppCommand"/>. The new app and the move commit together.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="TransactionRunner"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UnlinkStoreAppCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                           IStoreasyRepositoryBase<App> AppRepository,
                                           ITransactionRunner TransactionRunner,
                                           IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UnlinkStoreAppCommand, int>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UnlinkStoreAppCommand request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId,
                                                                        projection: s => new { s.Id, s.AppId, s.Store, s.ExternalId, s.Name, Siblings = s.App.StoreApps.Count(o => o.Id != s.Id) },
                                                                        cancellationToken: cancellationToken);

        if (storeApp == null)
            return Response<int>.Error(0, MessageKey.StoreAppNotFound);

        if (storeApp.Siblings == 0)
            return Response<int>.Success(storeApp.AppId);

        var app = new App { OrganizationId = request.OrganizationId, Name = storeApp.Name ?? storeApp.ExternalId };

        await _transactionRunner.RunAsync(async () =>
        {
            await _appRepository.AddAsync(app, cancellationToken);

            await _storeAppRepository.ExecuteUpdateAsync(storeApp.Id, new SetPropertyBuilder<StoreApp>().SetPropertyValue(s => s.AppId, app.Id), cancellationToken);
        });

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.StoreAppUnlinked,
                                     storeApp.Id.ToString(),
                                     $"{storeApp.Store} record {storeApp.ExternalId} split from app {storeApp.AppId} into app {app.Id}.",
                                     storeApp.Store,
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(app.Id);
    }
}
