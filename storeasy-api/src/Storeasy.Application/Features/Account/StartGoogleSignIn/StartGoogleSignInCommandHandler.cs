using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.StartGoogleSignIn;

/// <summary>
/// Handles <see cref="StartGoogleSignInCommand"/>.
/// </summary>
/// <param name="GoogleSignIn"></param>
public record StartGoogleSignInCommandHandler(IGoogleSignIn GoogleSignIn) : IInterceptable, ICommandHandler<StartGoogleSignInCommand, GoogleSignInStartDto>
{
    private readonly IGoogleSignIn _googleSignIn = GoogleSignIn;

    /// <inheritdoc/>
    public async Task<Response<GoogleSignInStartDto>> Handle(StartGoogleSignInCommand request, CancellationToken cancellationToken)
    {
        if (!_googleSignIn.IsEnabled)
            return Response<GoogleSignInStartDto>.Error(null, MessageKey.GoogleSignInDisabled);

        var started = await _googleSignIn.StartAsync(cancellationToken);

        return started == null ? Response<GoogleSignInStartDto>.Error(null, MessageKey.GoogleSignInUnavailable) : Response<GoogleSignInStartDto>.Success(started);
    }
}
