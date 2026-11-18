using Microsoft.AspNetCore.Http;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.GetSessionList;

/// <summary>
/// Handles <see cref="GetSessionListQuery"/>. Expired sessions are left out; tokens never leave the server.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="currentUser"></param>
/// <param name="httpContextAccessor"></param>
/// <param name="timeProvider"></param>
public class GetSessionListQueryHandler(IStoreasyRepositoryBase<UserSession> sessionRepository,
                                        ICurrentUser currentUser,
                                        IHttpContextAccessor httpContextAccessor,
                                        TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetSessionListQuery, List<AccountSessionDto>>
{
    private readonly IStoreasyRepositoryBase<UserSession> _sessionRepository = sessionRepository;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<AccountSessionDto>>> Handle(GetSessionListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var currentToken = _httpContextAccessor.GetTokenFromHeader();

        var sessions = await _sessionRepository.GetAllAsync(s => s.UserId == userId && s.ExpiryDate > now,
                                                            projection: s => new UserSession { Id = s.Id, AccessToken = s.AccessToken, DeviceId = s.DeviceId, UserAgent = s.UserAgent, IpAddress = s.IpAddress, CreationDate = s.CreationDate, ExpiryDate = s.ExpiryDate },
                                                            cancellationToken: cancellationToken);

        return Response<List<AccountSessionDto>>.Success([.. sessions.Select(s => new AccountSessionDto
        {
            Id = s.Id,
            DeviceId = s.DeviceId,
            UserAgent = s.UserAgent,
            IpAddress = s.IpAddress,
            LastSeenDate = s.CreationDate,
            ExpiryDate = s.ExpiryDate,
            IsCurrent = !string.IsNullOrEmpty(currentToken) && s.AccessToken == currentToken,
        }).OrderByDescending(s => s.IsCurrent).ThenByDescending(s => s.LastSeenDate)]);
    }
}
