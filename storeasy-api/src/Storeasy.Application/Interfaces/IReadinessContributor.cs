using Storeasy.Application.Dtos.ReadinessDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Adds items to the readiness check of a store record beside the content checks (e.g. subscriptions and In-App
/// Purchases that go to review with the version). A contributor never fails the check: what it cannot read becomes an
/// <see cref="ReadinessStatus.Unknown"/> item.
/// </summary>
public interface IReadinessContributor
{
    /// <summary>
    /// Items of the record.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="content">The content read of the check.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<ReadinessItemDto>> EvaluateAsync(StoreAppContext app, StoreContentSnapshot content, CancellationToken cancellationToken);
}
