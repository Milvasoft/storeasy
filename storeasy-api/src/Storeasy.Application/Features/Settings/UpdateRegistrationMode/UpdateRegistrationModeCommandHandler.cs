using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Settings.UpdateRegistrationMode;

/// <summary>
/// Handles <see cref="UpdateRegistrationModeCommand"/>.
/// </summary>
/// <param name="RegistrationModes"></param>
[Log]
public record UpdateRegistrationModeCommandHandler(IRegistrationModes RegistrationModes) : IInterceptable, ICommandHandler<UpdateRegistrationModeCommand, RegistrationMode>
{
    private readonly IRegistrationModes _registrationModes = RegistrationModes;

    /// <inheritdoc/>
    public async Task<Response<RegistrationMode>> Handle(UpdateRegistrationModeCommand request, CancellationToken cancellationToken)
    {
        await _registrationModes.SetAsync(request.Mode, cancellationToken);

        return Response<RegistrationMode>.Success((await _registrationModes.GetAsync(cancellationToken)).Mode);
    }
}
