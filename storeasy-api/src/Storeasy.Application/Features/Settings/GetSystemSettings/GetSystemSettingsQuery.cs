using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.SettingsDtos;

namespace Storeasy.Application.Features.Settings.GetSystemSettings;

/// <summary>
/// Reads the instance settings a platform manager may change (the registration mode and where it comes from).
/// </summary>
public record GetSystemSettingsQuery : IQuery<SystemSettingsDto>
{
}
