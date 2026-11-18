using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ReadinessDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Runs the readiness check of one store record (docs/urun-plani.md 11.1): one live read of the store, evaluated with the
/// rules catalog, the app profile, the saved answers and the assisted steps. The summary is kept on the record for the
/// organization overview.
/// </summary>
public interface IReadinessService
{
    /// <summary>
    /// Checks <paramref name="app"/> now.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ReadinessReportDto>> CheckAsync(StoreAppContext app, CancellationToken cancellationToken);

    /// <summary>
    /// Assisted step state of the organization's apps by (app, store), then by step key, with the step id.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="appId">One app, or every app when null.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Dictionary<(int AppId, StoreKind Store), Dictionary<string, (int Id, AssistedStepStatus Status)>>> GetStepsAsync(int organizationId, int? appId, CancellationToken cancellationToken);
}
