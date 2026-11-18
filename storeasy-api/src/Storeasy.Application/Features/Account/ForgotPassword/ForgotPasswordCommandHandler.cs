using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.ForgotPassword;

/// <summary>
/// Handles <see cref="ForgotPasswordCommand"/>. Always succeeds; an unknown address is silently ignored.
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="AccountMailer"></param>
[Log]
public record ForgotPasswordCommandHandler(IStoreasyRepositoryBase<User> UserRepository, IAccountMailer AccountMailer) : IInterceptable, ICommandHandler<ForgotPasswordCommand>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IAccountMailer _accountMailer = AccountMailer;

    /// <inheritdoc/>
    public async Task<Response> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await _userRepository.GetFirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail,
                                                                projection: u => new { u.Id, u.Email, u.Name },
                                                                cancellationToken: cancellationToken);

        if (user != null)
            await _accountMailer.SendPasswordResetAsync(user.Id, user.Email, user.Name, cancellationToken);

        return Response.Success();
    }
}
