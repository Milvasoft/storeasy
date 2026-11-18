using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetMsSubmissionList;

/// <summary>
/// Lists the Microsoft Store submissions storeasy knows for a product (app, flight, add-on and MSI/EXE submissions), newest first, with their status history and the "changed in Partner Center" flag. Database only; the sync and storeasy's own writes keep it current.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMsSubmissionListQuery : IQuery<List<MsSubmissionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
