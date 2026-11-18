using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Features.MsStore.AddMsProduct;

/// <summary>
/// Adds an MSI/EXE product by its Store id: the MSI/EXE submission API has no call that lists products (MSIX apps come
/// with the sync). The record is written unconfirmed, then a sync job reads the product with the connection and removes
/// the record when the store refuses it.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record AddMsProductCommand : ICommand<MsProductAddedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft connection to read the product with.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Store id of the product (12 characters, e.g. 9P4K7Q2MZL1D).
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Display name until the sync reads the store; the product id when empty.
    /// </summary>
    public string Name { get; set; }
}
