using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.SettingsDtos;

namespace Storeasy.Application.Features.Settings.GetPublicSettings;

/// <summary>
/// Reads the instance settings the sign in and settings pages need (anonymous): registration mode, Google sign in, the API version and the
/// web push public key.
/// </summary>
public record GetPublicSettingsQuery : IQuery<PublicSettingsDto>
{
}
