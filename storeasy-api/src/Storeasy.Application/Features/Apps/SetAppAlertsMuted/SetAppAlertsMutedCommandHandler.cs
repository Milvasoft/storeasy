using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.SetAppAlertsMuted;

/// <summary>
/// Handles <see cref="SetAppAlertsMutedCommand"/>. An app already in the asked state is left as it is (the first mute
/// date stays) and nothing is audited; an app of another organization is reported as not found.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record SetAppAlertsMutedCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                              IAuditTrail AuditTrail,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SetAppAlertsMutedCommand, int>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SetAppAlertsMutedCommand request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: a => new App { Id = a.Id, Name = a.Name, AlertsMutedDate = a.AlertsMutedDate },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        if ((app.AlertsMutedDate != null) == request.Muted)
            return Response<int>.Success(app.Id);

        DateTime? mutedDate = request.Muted ? _timeProvider.GetUtcNow().UtcDateTime : null;

        await _appRepository.ExecuteUpdateAsync(a => a.Id == app.Id && a.OrganizationId == request.OrganizationId,
                                                new SetPropertyBuilder<App>().SetPropertyValue(a => a.AlertsMutedDate, mutedDate),
                                                cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     request.Muted ? AuditAction.AppAlertsMuted : AuditAction.AppAlertsUnmuted,
                                     app.Id.ToString(),
                                     request.Muted ? $"Alerts of app {app.Id} ({app.Name}) muted." : $"Alerts of app {app.Id} ({app.Name}) turned on again.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(app.Id);
    }
}
