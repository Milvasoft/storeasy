using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.ForgotPassword;

/// <summary>
/// Handles <see cref="ForgotPasswordCommand"/>. Always succeeds and does the same work for every address: it only queues
/// the request; the background e-mail sender looks the account up and mails the link when one exists. The answer time
/// therefore does not reveal who has an account.
/// </summary>
/// <param name="AccountMailer"></param>
[Log]
public record ForgotPasswordCommandHandler(IAccountMailer AccountMailer) : IInterceptable, ICommandHandler<ForgotPasswordCommand>
{
    private readonly IAccountMailer _accountMailer = AccountMailer;

    /// <inheritdoc/>
    public async Task<Response> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        await _accountMailer.RequestPasswordResetAsync(request.Email, cancellationToken);

        return Response.Success();
    }
}
