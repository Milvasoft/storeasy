using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.SettingsDtos;

namespace Storeasy.Application.Features.Settings.GetSystemStatus;

/// <summary>
/// Reads the instance status: build, dependencies, configured features (yes or no only) and the job queues.
/// </summary>
public record GetSystemStatusQuery : IQuery<SystemStatusDto>
{
}
