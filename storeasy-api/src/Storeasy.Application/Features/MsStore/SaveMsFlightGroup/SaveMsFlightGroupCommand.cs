using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.MsStore.SaveMsFlightGroup;

/// <summary>
/// Records the name (and member count) of a Partner Center flight group. The API names flight groups only by id; groups
/// are created and filled in Partner Center (assisted step).
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record SaveMsFlightGroupCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Partner Center flight group id.
    /// </summary>
    public string GroupId { get; set; }

    /// <summary>
    /// Group name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Members, when known.
    /// </summary>
    public int? MemberCount { get; set; }
}
