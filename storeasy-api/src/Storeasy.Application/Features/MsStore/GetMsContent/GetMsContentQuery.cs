using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.GetMsContent;

/// <summary>
/// Reads the current Microsoft Store content of a product (or one of its flights or add-ons) from the store: listings,
/// images, packages, price and availability, rollout, MSI/EXE installer packages. The content comes from the pending
/// submission when there is one, otherwise from the last published one.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMsContentQuery : IQuery<MsContentDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Flight to read instead of the app submission.
    /// </summary>
    public string FlightId { get; set; }

    /// <summary>
    /// Add-on to read instead of the app submission.
    /// </summary>
    public string AddonStoreId { get; set; }
}
