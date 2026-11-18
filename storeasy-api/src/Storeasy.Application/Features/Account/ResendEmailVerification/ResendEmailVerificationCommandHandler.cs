using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.ResendEmailVerification;

/// <summary>
/// Handles <see cref="ResendEmailVerificationCommand"/>. The account is always the caller's own (from the token).
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AccountMailer"></param>
[Log]
public record ResendEmailVerificationCommandHandler(IStoreasyRepositoryBase<User> UserRepository,
                                                    ICurrentUser CurrentUser,
                                                    IAccountMailer AccountMailer) : IInterceptable, ICommandHandler<ResendEmailVerificationCommand>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAccountMailer _accountMailer = AccountMailer;

    /// <inheritdoc/>
    public async Task<Response> Handle(ResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        var user = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == userId,
                                                                projection: u => new { u.Id, u.Email, u.Name, u.EmailConfirmed },
                                                                cancellationToken: cancellationToken);

        if (user == null)
            return Response.Error(MessageKey.UserNotFound);

        // A user without an e-mail (the seeded root user) has nothing to verify.
        if (user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
            return Response.Error(MessageKey.EmailAlreadyVerified);

        await _accountMailer.SendEmailVerificationAsync(user.Id, user.Email, user.Name, cancellationToken);

        return Response.Success();
    }
}
