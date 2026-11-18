using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.AccountDetail;

/// <summary>
/// Handles the query for retrieving the account details. The account is always the authenticated
/// caller's own: the id comes from the token, never from the request.
/// </summary>
/// <param name="userRepository"></param>
/// <param name="externalLoginRepository"></param>
/// <param name="currentUser"></param>
public class AccountDetailQueryHandler(IStoreasyRepositoryBase<User> userRepository,
                                       IStoreasyRepositoryBase<UserExternalLogin> externalLoginRepository,
                                       ICurrentUser currentUser) : IInterceptable, IQueryHandler<AccountDetailQuery, AccountDetailDto>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;
    private readonly IStoreasyRepositoryBase<UserExternalLogin> _externalLoginRepository = externalLoginRepository;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public async Task<Response<AccountDetailDto>> Handle(AccountDetailQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.GetRequiredId();

        var user = await _userRepository.GetByIdAsync(currentUserId, projection: AccountDetailDto.Projection, cancellationToken: cancellationToken);

        if (user == null)
            return Response<AccountDetailDto>.Error(null, MessageKey.UserNotFound);

        user.Permissions = PlatformPermissions.Expand(user.Permissions);

        user.ExternalLogins = [.. (await _externalLoginRepository.GetAllAsync(l => l.UserId == currentUserId,
                                                                              projection: l => new UserExternalLogin { Provider = l.Provider },
                                                                              cancellationToken: cancellationToken)).Select(l => l.Provider).Distinct().Order()];

        return Response<AccountDetailDto>.Success(user);
    }
}
