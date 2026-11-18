using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Ef.Transaction;

namespace Storeasy.Application.Features.Account.RevokeSession;

/// <summary>
/// Handles <see cref="RevokeSessionCommand"/>. Another user's session is reported as not found.
/// </summary>
/// <param name="SessionRepository"></param>
/// <param name="HistoryRepository"></param>
/// <param name="RevocationList"></param>
/// <param name="RealtimeConnections"></param>
/// <param name="CurrentUser"></param>
[Transaction]
public record RevokeSessionCommandHandler(IStoreasyRepositoryBase<UserSession> SessionRepository,
                                          IStoreasyRepositoryBase<UserSessionHistory> HistoryRepository,
                                          ITokenRevocationList RevocationList,
                                          IRealtimeConnections RealtimeConnections,
                                          ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<RevokeSessionCommand, long>
{
    private readonly IStoreasyRepositoryBase<UserSession> _sessionRepository = SessionRepository;
    private readonly IStoreasyRepositoryBase<UserSessionHistory> _historyRepository = HistoryRepository;
    private readonly ITokenRevocationList _revocationList = RevocationList;
    private readonly IRealtimeConnections _realtimeConnections = RealtimeConnections;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        var session = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == request.Id && s.UserId == userId, UserSession.Projections.CurrentSession, cancellationToken: cancellationToken);

        if (session == null)
            return Response<long>.Error(0, MessageKey.SessionNotFound);

        await _sessionRepository.SignOutSessionsAsync(_historyRepository, _revocationList, _realtimeConnections, userId, [session], cancellationToken);

        return Response<long>.Success(session.Id);
    }
}
