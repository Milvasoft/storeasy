using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.ApplyMsChange;

/// <summary>
/// Plans and, when asked, applies one Microsoft Store change: listings, images, MSIX packages (app or flight), price and
/// availability, MSI/EXE installer packages, package rollout, a new flight, a new add-on, add-on content or discarding a
/// draft. With <see cref="DryRun"/> (the default) only the plan comes back: diff, remarks, store calls, the permission
/// applying needs and <see cref="MsChangePlanDto.BaseHash"/>. Applying sends the same change with that hash; it is
/// refused when the store content changed since, when the plan is blocked (single active submission, submission changed in
/// Partner Center, Pricing Version 2, 409 products) or when the caller's role lacks the plan's permission. The write itself
/// runs as a store job; submission edits stay in a draft until <c>SubmitMsSubmission</c>.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record ApplyMsChangeCommand : ICommand<MsChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The change.
    /// </summary>
    public MsChangeInputDto Change { get; set; }

    /// <summary>
    /// Only plan (default) or plan and apply.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Fingerprint of the plan the caller saw; required to apply.
    /// </summary>
    public string BaseHash { get; set; }
}
