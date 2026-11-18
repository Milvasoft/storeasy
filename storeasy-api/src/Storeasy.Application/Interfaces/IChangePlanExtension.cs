namespace Storeasy.Application.Interfaces;

/// <summary>
/// A family of change plans whose store state is not part of the content model (subscriptions, In-App Purchases,
/// purchase settings). The change plan service asks it to read the fingerprinted base again on approval, and the
/// <see cref="StoreJobKind.ApplyChangePlan"/> job asks it to write the plan; diff, permission, typed confirmation,
/// per field audit and live steps stay the same as for content plans.
/// </summary>
public interface IChangePlanExtension
{
    /// <summary>
    /// Whether plans of <paramref name="kind"/> belong to this extension.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    bool Handles(ChangePlanKind kind);

    /// <summary>
    /// Reads the store values <paramref name="baseKeys"/> name, as a content snapshot (values only).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="baseKeys"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken);

    /// <summary>
    /// Writes an approved plan (inside the apply job; every store call is a step).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Short outcome lines.</returns>
    Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Audit action of one item of a plan of <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item);
}

/// <summary>
/// An approved plan to write.
/// </summary>
/// <param name="OrganizationId">Organization.</param>
/// <param name="AppId">storeasy app.</param>
/// <param name="StoreAppId">Store record.</param>
/// <param name="PlanId">Plan.</param>
/// <param name="Kind">Plan kind.</param>
/// <param name="Store">Store of the connection.</param>
/// <param name="Target">Store target (the connection secret is opened by the job).</param>
/// <param name="Changes">The plan's changes as JSON.</param>
public sealed record ChangePlanApplyRequest(int OrganizationId, int AppId, int StoreAppId, long PlanId, ChangePlanKind Kind, StoreKind Store, StoreContentTarget Target, string Changes);
