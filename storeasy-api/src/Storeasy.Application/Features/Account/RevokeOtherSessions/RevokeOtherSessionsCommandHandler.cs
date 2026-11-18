using Microsoft.AspNetCore.Http;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Ef.Transaction;

namespace Storeasy.Application.Features.Account.RevokeOtherSessions;

/// <summary>
/// Handles <see cref="RevokeOtherSessionsCommand"/>. The current session is recognized by its access token.
/// </summary>
/// <param name="SessionRepository"></param>
/// <param name="HistoryRepository"></param>
/// <param name="RevocationList"></param>
/// <param name="RealtimeConnections"></param>
/// <param name="CurrentUser"></param>
/// <param name="HttpContextAccessor"></param>
[Transaction]
public record RevokeOtherSessionsCommandHandler(IStoreasyRepositoryBase<UserSession> SessionRepository,
                                               IStoreasyRepositoryBase<UserSessionHistory> HistoryRepository,
                                               ITokenRevocationList RevocationList,
                                               IRealtimeConnections RealtimeConnections,
                                               ICurrentUser CurrentUser,
                                               IHttpContextAccessor HttpContextAccessor) : IInterceptable, ICommandHandler<RevokeOtherSessionsCommand, int>
{
    private readonly IStoreasyRepositoryBase<UserSession> _sessionRepository = SessionRepository;
    private readonly IStoreasyRepositoryBase<UserSessionHistory> _historyRepository = HistoryRepository;
    private readonly ITokenRevocationList _revocationList = RevocationList;
    private readonly IRealtimeConnections _realtimeConnections = RealtimeConnections;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IHttpContextAccessor _httpContextAccessor = HttpContextAccessor;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RevokeOtherSessionsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var currentToken = _httpContextAccessor.GetTokenFromHeader();

        var others = await _sessionRepository.GetAllAsync(s => s.UserId == userId && s.AccessToken != currentToken, UserSession.Projections.CurrentSession, cancellationToken: cancellationToken);

        await _sessionRepository.SignOutSessionsAsync(_historyRepository, _revocationList, _realtimeConnections, userId, others, cancellationToken);

        return Response<int>.Success(others.Count);
    }
}
