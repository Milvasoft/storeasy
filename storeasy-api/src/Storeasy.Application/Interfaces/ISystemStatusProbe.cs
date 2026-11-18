using Storeasy.Application.Dtos.SettingsDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Checks the instance's dependencies and reads the queue numbers for the platform status screen.
/// </summary>
public interface ISystemStatusProbe
{
    /// <summary>
    /// Runs the checks (each with a short timeout) and returns the status.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<SystemStatusDto> CheckAsync(CancellationToken cancellationToken);
}
