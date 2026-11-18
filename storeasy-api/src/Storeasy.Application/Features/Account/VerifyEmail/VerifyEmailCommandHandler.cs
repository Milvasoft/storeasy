using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.VerifyEmail;

/// <summary>
/// Handles <see cref="VerifyEmailCommand"/>. The token is single use (<see cref="RepositoryExtensions.ConsumeAccountTokenAsync"/>).
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="UserRepository"></param>
/// <param name="TimeProvider"></param>
[Log]
public record VerifyEmailCommandHandler(IStoreasyRepositoryBase<AccountToken> TokenRepository,
                                        IStoreasyRepositoryBase<User> UserRepository,
                                        TimeProvider TimeProvider) : IInterceptable, ICommandHandler<VerifyEmailCommand>
{
    private readonly IStoreasyRepositoryBase<AccountToken> _tokenRepository = TokenRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var userId = await _tokenRepository.ConsumeAccountTokenAsync(request.Token, AccountTokenPurpose.EmailVerification, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        if (userId == null)
            return Response.Error(MessageKey.AccountTokenInvalid);

        await _userRepository.ExecuteUpdateAsync(userId.Value, new SetPropertyBuilder<User>().SetPropertyValue(u => u.EmailConfirmed, true), cancellationToken);

        return Response.Success();
    }
}
