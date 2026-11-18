using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Features.PurchaseSettings.GetSandboxTesterList;

/// <summary>
/// Lists the App Store sandbox testers of the store record's developer account, live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSandboxTesterListQuery : IQuery<List<SandboxTesterDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }
}
