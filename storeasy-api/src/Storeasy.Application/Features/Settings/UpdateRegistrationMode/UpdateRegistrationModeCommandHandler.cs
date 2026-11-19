using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.Settings.UpdateRegistrationMode;

/// <summary>
/// Handles <see cref="UpdateRegistrationModeCommand"/>. A single team instance is always invite only, so the mode
/// cannot be changed there (<see cref="MessageKey.RegistrationModeFixedInSingleTeam"/>).
/// </summary>
/// <param name="RegistrationModes"></param>
/// <param name="DeploymentOptions"></param>
[Log]
public record UpdateRegistrationModeCommandHandler(IRegistrationModes RegistrationModes, DeploymentOptions DeploymentOptions) : IInterceptable, ICommandHandler<UpdateRegistrationModeCommand, RegistrationMode>
{
    private readonly IRegistrationModes _registrationModes = RegistrationModes;
    private readonly DeploymentOptions _deploymentOptions = DeploymentOptions;

    /// <inheritdoc/>
    public async Task<Response<RegistrationMode>> Handle(UpdateRegistrationModeCommand request, CancellationToken cancellationToken)
    {
        if (_deploymentOptions.IsSingleTeam)
            return Response<RegistrationMode>.Error(RegistrationMode.InviteOnly, MessageKey.RegistrationModeFixedInSingleTeam);

        await _registrationModes.SetAsync(request.Mode, cancellationToken);

        return Response<RegistrationMode>.Success((await _registrationModes.GetAsync(cancellationToken)).Mode);
    }
}
