using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.ReorderApps;

/// <summary>
/// Handles <see cref="ReorderAppsCommand"/>. The new places are written in one statement (the listed apps take their index,
/// the apps placed before that the order leaves out lose their place), and an order that is already in place writes and
/// audits nothing. An id that is not an app of the organization (of another organization, or deleted) is reported as not
/// found and nothing changes.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record ReorderAppsCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                        IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<ReorderAppsCommand, int>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(ReorderAppsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.AppIds.ToArray();

        // The listed apps, and the apps placed before that the new order may leave out.
        var rows = await _appRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && (ids.Contains(a.Id) || a.DisplayOrder != null),
                                                    projection: a => new App { Id = a.Id, DisplayOrder = a.DisplayOrder },
                                                    cancellationToken: cancellationToken);

        var current = rows.ToDictionary(a => a.Id, a => a.DisplayOrder);

        if (!Array.TrueForAll(ids, current.ContainsKey))
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var moved = ids.Where((id, index) => current[id] != index).Count();
        var unplaced = current.Count(app => app.Value != null && !ids.Contains(app.Key));

        if (moved == 0 && unplaced == 0)
            return Response<int>.Success(ids.Length);

        await _appRepository.ExecuteUpdateAsync(a => a.OrganizationId == request.OrganizationId && (ids.Contains(a.Id) || a.DisplayOrder != null),
                                                new SetPropertyBuilder<App>().SetProperty(a => a.DisplayOrder, a => ids.Contains(a.Id) ? Array.IndexOf(ids, a.Id) : (int?)null),
                                                cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AppsReordered,
                                     request.OrganizationId.ToString(),
                                     $"Apps list reordered: {ids.Length} apps placed, {moved} moved, {unplaced} left unplaced.",
                                     details: new { AppIds = ids },
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(ids.Length);
    }
}
