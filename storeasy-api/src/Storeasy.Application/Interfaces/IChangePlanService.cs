using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Saves change plans and approves them (docs/urun-plani.md 2.2: every write goes through a plan). Approval checks the
/// plan's own permission and typed confirmation, reads the store again and refuses when the touched values changed since
/// the plan was made, then queues the apply job. Shared by every planning and applying request, the web and MCP alike.
/// </summary>
public interface IChangePlanService
{
    /// <summary>
    /// Saves <paramref name="draft"/> as a pending plan of <paramref name="app"/>. With <paramref name="dryRun"/> false the
    /// plan is approved right away (same checks as <see cref="ApproveAsync"/>, <paramref name="confirm"/> included).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="snapshot">Store content the draft was made from (its values are fingerprinted).</param>
    /// <param name="draft"></param>
    /// <param name="dryRun"></param>
    /// <param name="confirm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ChangePlanDto>> SaveAsync(StoreAppContext app, StoreContentSnapshot snapshot, PlanDraft draft, bool dryRun, string confirm, CancellationToken cancellationToken);

    /// <summary>
    /// Approves a pending plan and queues its apply job.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="planId"></param>
    /// <param name="confirm">Typed confirmation, for plans that need one.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ChangePlanDto>> ApproveAsync(int organizationId, long planId, string confirm, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one plan of the organization with its derived state and the apply job's error; null when not found.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="planId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChangePlanDto> GetAsync(int organizationId, long planId, CancellationToken cancellationToken);

    /// <summary>
    /// Closes a pending plan without writing anything: <see cref="ChangePlanStatus.Rejected"/> (needs the plan's own
    /// permission, with a reason) or <see cref="ChangePlanStatus.Discarded"/>.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="planId"></param>
    /// <param name="status"></param>
    /// <param name="note"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ChangePlanDto>> DecideAsync(int organizationId, long planId, ChangePlanStatus status, string note, CancellationToken cancellationToken);
}
