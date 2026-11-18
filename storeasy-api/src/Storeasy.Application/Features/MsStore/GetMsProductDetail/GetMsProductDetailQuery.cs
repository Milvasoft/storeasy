using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetMsProductDetail;

/// <summary>
/// Reads a Microsoft Store product of the organization: its flags, its active submission (with the "changed in Partner
/// Center" warning), the Partner Center rules and its assisted steps. Database only, no store call.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMsProductDetailQuery : IQuery<MsProductDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
