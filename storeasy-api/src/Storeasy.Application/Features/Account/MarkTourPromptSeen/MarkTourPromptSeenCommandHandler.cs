using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.MarkTourPromptSeen;

/// <summary>
/// Handles <see cref="MarkTourPromptSeenCommand"/> with one conditional update of the caller's row. Repeating it keeps
/// the first date.
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
[Log]
public record MarkTourPromptSeenCommandHandler(IStoreasyRepositoryBase<User> UserRepository,
                                               ICurrentUser CurrentUser,
                                               TimeProvider TimeProvider) : IInterceptable, ICommandHandler<MarkTourPromptSeenCommand, int>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(MarkTourPromptSeenCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        DateTime? now = _timeProvider.GetUtcNow().UtcDateTime;

        await _userRepository.ExecuteUpdateAsync(u => u.Id == userId && u.TourPromptSeenDate == null,
                                                 new SetPropertyBuilder<User>().SetPropertyValue(u => u.TourPromptSeenDate, now),
                                                 cancellationToken);

        return Response<int>.Success(userId);
    }
}
