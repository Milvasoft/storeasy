using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.OpenRejectionCase;

/// <summary>
/// Handles <see cref="OpenRejectionCaseCommand"/>.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
[Log]
public record OpenRejectionCaseCommandHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                              IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                              IAuditTrail AuditTrail,
                                              ICurrentUser CurrentUser,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<OpenRejectionCaseCommand, int>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(OpenRejectionCaseCommand request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == request.AppId && s.OrganizationId == request.OrganizationId && s.Store == request.Store && !s.App.IsDeleted,
                                                                        projection: s => new { s.Id, s.AppId },
                                                                        cancellationToken: cancellationToken);

        if (storeApp == null)
            return Response<int>.Error(0, MessageKey.StoreAppNotFound);

        var versionString = request.VersionString.Trim();

        var open = await _caseRepository.GetFirstOrDefaultAsync(c => c.StoreAppId == storeApp.Id && c.VersionString == versionString && c.State == RejectionState.Open,
                                                                projection: c => (int?)c.Id,
                                                                cancellationToken: cancellationToken);

        if (open.HasValue)
            return Response<int>.Error(0, MessageKey.RejectionCaseExists);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rejectionCase = RejectionCases.Open(request.OrganizationId, storeApp.AppId, storeApp.Id, request.Store, null, versionString, request.BuildNumber?.Trim(), request.Kind, RejectionSource.Manual, now, _auditTrail.Channel, _currentUser.UserName);

        await _caseRepository.AddAsync(rejectionCase, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RejectionOpened, rejectionCase.Id.ToString(), $"Rejection case of {versionString} opened by hand.", request.Store, cancellationToken: cancellationToken);

        return Response<int>.Success(rejectionCase.Id);
    }
}
