namespace Storeasy.Application.Interfaces;

/// <summary>
/// Applies App Store version state changes, whether a webhook or the periodic sync saw them: keeps the last state of each
/// version, opens a rejection case (and notifies the owners and admins) when a version goes to REJECTED or
/// METADATA_REJECTED, closes the open cases of a store record when it is submitted again, and publishes the change.
/// A state seen twice is applied once.
/// </summary>
public interface IVersionStateTracker
{
    /// <summary>
    /// Applies <paramref name="changes"/> (any order, any store records of one organization) and returns what happened.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="changes"></param>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<VersionStateOutcome>> ApplyAsync(int organizationId, IReadOnlyList<VersionStateChange> changes, RejectionSource source, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the open rejection cases of a store record because it was submitted again through storeasy (phase 2's
    /// submit for review calls this after a successful submission). Returns the number closed.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> CloseForSubmissionAsync(int organizationId, int storeAppId, CancellationToken cancellationToken);
}

/// <summary>
/// A version state as the store reports it.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
/// <param name="ExternalVersionId">The store's id of the version.</param>
/// <param name="VersionString">User facing version.</param>
/// <param name="BuildNumber">Build attached, if known.</param>
/// <param name="State">New state (store spelling).</param>
/// <param name="Date">UTC instant of the change, if known.</param>
public sealed record VersionStateChange(int StoreAppId, string ExternalVersionId, string VersionString, string BuildNumber, string State, DateTime? Date);

/// <summary>
/// What applying one change did.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
/// <param name="ExternalVersionId">Version.</param>
/// <param name="PreviousState">State before (null when the version was new).</param>
/// <param name="State">State after.</param>
/// <param name="Changed">Whether the state changed.</param>
/// <param name="OpenedCaseId">Rejection case opened, if one.</param>
/// <param name="ClosedCaseIds">Rejection cases closed.</param>
public sealed record VersionStateOutcome(int StoreAppId, string ExternalVersionId, string PreviousState, string State, bool Changed, int? OpenedCaseId, IReadOnlyList<int> ClosedCaseIds);
