using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.RemovePackageGroup;

/// <summary>
/// Removes a package group; its packages stay tracked without a group.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record RemovePackageGroupCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Group.
    /// </summary>
    public int Id { get; set; }
}
