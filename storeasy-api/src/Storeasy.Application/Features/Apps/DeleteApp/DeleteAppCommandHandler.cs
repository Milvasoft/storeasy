using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.DeleteApp;

/// <summary>
/// Handles <see cref="DeleteAppCommand"/>. The records and the app are deleted together; the database removes what hangs on
/// them (builds, Chrome items, Firefox add-ons, winget packages, plans). An app of another organization is reported as not
/// found.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="TransactionRunner"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteAppCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                      IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                      ITransactionRunner TransactionRunner,
                                      IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteAppCommand, int>
{
    /// <summary>
    /// Stores whose records are added by id and never listed by a sync, so deleting them cannot bring them back by itself.
    /// </summary>
    public static readonly IReadOnlyList<StoreKind> DeletableStores = [StoreKind.Chrome, StoreKind.Firefox, StoreKind.Winget];

    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, Name = a.Name },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var records = await _storeAppRepository.GetAllAsync(s => s.AppId == app.Id && s.OrganizationId == request.OrganizationId,
                                                            projection: s => new StoreApp { Id = s.Id, Store = s.Store, ExternalId = s.ExternalId },
                                                            cancellationToken: cancellationToken);

        if (records.Any(r => !DeletableStores.Contains(r.Store)))
            return Response<int>.Error(0, MessageKey.AppDeleteNotAllowed);

        await _transactionRunner.RunAsync(async () =>
        {
            await _storeAppRepository.ExecuteDeleteAsync(s => s.AppId == app.Id && s.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

            await _appRepository.ExecuteDeleteAsync(a => a.Id == app.Id && a.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);
        });

        var removed = records.Count == 0 ? "no store records" : string.Join(", ", records.Select(r => $"{r.Store} {r.ExternalId}"));

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AppDeleted,
                                     app.Id.ToString(),
                                     $"App {app.Id} ({app.Name}) deleted with {removed}.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(app.Id);
    }
}
