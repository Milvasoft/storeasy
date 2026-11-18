using Milvasoft.Components.CQRS.Query;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.GetAssistedStepList;

/// <summary>
/// Lists the assisted steps of the organization's apps (of one app, store or state when filtered). The steps that apply to each store record (built in steps and assisted catalog rules) are created on first read.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAssistedStepListQuery : IQuery<List<AssistedStepListDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only steps of this app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Only steps of this store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Only steps in this state.
    /// </summary>
    public AssistedStepStatus? Status { get; set; }
}
