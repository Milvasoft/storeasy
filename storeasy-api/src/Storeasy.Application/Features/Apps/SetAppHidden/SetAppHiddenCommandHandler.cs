using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.SetAppHidden;

/// <summary>
/// Handles <see cref="SetAppHiddenCommand"/>. An app already in the asked state is left as it is (the first hide date
/// stays) and nothing is audited; an app of another organization is reported as not found.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record SetAppHiddenCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                         IAuditTrail AuditTrail,
                                         TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SetAppHiddenCommand, int>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SetAppHiddenCommand request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: a => new App { Id = a.Id, Name = a.Name, HiddenDate = a.HiddenDate },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        if ((app.HiddenDate != null) == request.Hidden)
            return Response<int>.Success(app.Id);

        DateTime? hiddenDate = request.Hidden ? _timeProvider.GetUtcNow().UtcDateTime : null;

        await _appRepository.ExecuteUpdateAsync(a => a.Id == app.Id && a.OrganizationId == request.OrganizationId,
                                                new SetPropertyBuilder<App>().SetPropertyValue(a => a.HiddenDate, hiddenDate),
                                                cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     request.Hidden ? AuditAction.AppHidden : AuditAction.AppUnhidden,
                                     app.Id.ToString(),
                                     request.Hidden ? $"App {app.Id} ({app.Name}) hidden from the apps list." : $"App {app.Id} ({app.Name}) shown in the apps list again.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(app.Id);
    }
}
