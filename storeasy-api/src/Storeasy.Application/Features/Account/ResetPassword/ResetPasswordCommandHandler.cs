using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Identity.Abstract;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.ResetPassword;

/// <summary>
/// Handles <see cref="ResetPasswordCommand"/>. The token is used first (single use, conditional update). The password,
/// the cleared lockout, the session revocation and the retirement of the other reset links commit together; the
/// revocation stamp is rotated after the commit, so every access token issued before stops working at once.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="UserRepository"></param>
/// <param name="UserSessionRepository"></param>
/// <param name="UserSessionHistoriesRepository"></param>
/// <param name="UserManager"></param>
/// <param name="SecurityStampStore"></param>
/// <param name="TransactionRunner"></param>
/// <param name="TimeProvider"></param>
[Log]
public record ResetPasswordCommandHandler(IStoreasyRepositoryBase<AccountToken> TokenRepository,
                                          IStoreasyRepositoryBase<User> UserRepository,
                                          IStoreasyRepositoryBase<UserSession> UserSessionRepository,
                                          IStoreasyRepositoryBase<UserSessionHistory> UserSessionHistoriesRepository,
                                          IMilvaUserManager<User, int> UserManager,
                                          ISecurityStampStore SecurityStampStore,
                                          ITransactionRunner TransactionRunner,
                                          TimeProvider TimeProvider) : IInterceptable, ICommandHandler<ResetPasswordCommand>
{
    private readonly IStoreasyRepositoryBase<AccountToken> _tokenRepository = TokenRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IStoreasyRepositoryBase<UserSession> _userSessionRepository = UserSessionRepository;
    private readonly IStoreasyRepositoryBase<UserSessionHistory> _userSessionHistoriesRepository = UserSessionHistoriesRepository;
    private readonly IMilvaUserManager<User, int> _userManager = UserManager;
    private readonly ISecurityStampStore _securityStampStore = SecurityStampStore;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var userId = await _tokenRepository.ConsumeAccountTokenAsync(request.Token, AccountTokenPurpose.PasswordReset, now, cancellationToken);

        if (userId == null)
            return Response.Error(MessageKey.AccountTokenInvalid);

        var user = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == userId.Value, User.Projections.ChangePassword, cancellationToken: cancellationToken);

        if (user == null)
            return Response.Error(MessageKey.AccountTokenInvalid);

        // SetPasswordHash also rotates the persisted SecurityStamp (Identity tokens issued before stop working).
        _userManager.SetPasswordHash(user, request.NewPassword);

        user.EmailConfirmed = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        await _transactionRunner.RunAsync(async () =>
        {
            await _userRepository.UpdateAsync(user, cancellationToken, u => u.PasswordHash, u => u.SecurityStamp, u => u.EmailConfirmed, u => u.AccessFailedCount, u => u.LockoutEnd);

            await _userSessionRepository.RevokeAllSessionsAsync(_userSessionHistoriesRepository, user, cancellationToken);

            await _tokenRepository.RetireAccountTokensAsync(user.Id, AccountTokenPurpose.PasswordReset, now, cancellationToken);
        });

        await _securityStampStore.RotateAsync(user.Id, cancellationToken);

        return Response.Success();
    }
}
