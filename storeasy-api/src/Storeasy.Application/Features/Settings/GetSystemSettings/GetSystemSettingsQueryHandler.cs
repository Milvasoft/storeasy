using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.Settings.GetSystemSettings;

/// <summary>
/// Handles <see cref="GetSystemSettingsQuery"/>.
/// </summary>
/// <param name="registrationModes"></param>
/// <param name="registrationOptions"></param>
/// <param name="deploymentOptions"></param>
public class GetSystemSettingsQueryHandler(IRegistrationModes registrationModes, RegistrationOptions registrationOptions, DeploymentOptions deploymentOptions) : IInterceptable, IQueryHandler<GetSystemSettingsQuery, SystemSettingsDto>
{
    private readonly DeploymentOptions _deploymentOptions = deploymentOptions;
    private readonly IRegistrationModes _registrationModes = registrationModes;
    private readonly RegistrationOptions _registrationOptions = registrationOptions;

    /// <inheritdoc/>
    public async Task<Response<SystemSettingsDto>> Handle(GetSystemSettingsQuery request, CancellationToken cancellationToken)
    {
        var (mode, saved) = await _registrationModes.GetAsync(cancellationToken);

        return Response<SystemSettingsDto>.Success(new SystemSettingsDto { RegistrationMode = mode, RegistrationModeSaved = saved, ConfiguredRegistrationMode = _registrationOptions.Mode, Mode = _deploymentOptions.Mode });
    }
}
