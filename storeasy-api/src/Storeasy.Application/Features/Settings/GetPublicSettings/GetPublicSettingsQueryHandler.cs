using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.Settings.GetPublicSettings;

/// <summary>
/// Handles <see cref="GetPublicSettingsQuery"/>: the registration mode in force (saved or configured) and the bound options.
/// </summary>
/// <param name="registrationModes"></param>
/// <param name="googleAuthOptions"></param>
/// <param name="webPushOptions"></param>
/// <param name="buildOptions"></param>
public class GetPublicSettingsQueryHandler(IRegistrationModes registrationModes, GoogleAuthOptions googleAuthOptions, WebPushOptions webPushOptions, BuildOptions buildOptions) : IInterceptable, IQueryHandler<GetPublicSettingsQuery, PublicSettingsDto>
{
    private readonly IRegistrationModes _registrationModes = registrationModes;
    private readonly GoogleAuthOptions _googleAuthOptions = googleAuthOptions;
    private readonly WebPushOptions _webPushOptions = webPushOptions;
    private readonly BuildOptions _buildOptions = buildOptions;

    /// <inheritdoc/>
    public async Task<Response<PublicSettingsDto>> Handle(GetPublicSettingsQuery request, CancellationToken cancellationToken)
        => Response<PublicSettingsDto>.Success(new PublicSettingsDto
        {
            RegistrationMode = (await _registrationModes.GetAsync(cancellationToken)).Mode,
            GoogleSignInEnabled = _googleAuthOptions.Enabled,
            Version = _buildOptions.Version,
            WebPushPublicKey = _webPushOptions.Enabled ? _webPushOptions.PublicKey : null,
        });
}
