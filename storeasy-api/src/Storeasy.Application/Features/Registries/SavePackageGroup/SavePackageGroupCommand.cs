using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.SavePackageGroup;

/// <summary>
/// Adds (<see cref="Id"/> null) or changes a package group: its name and members. A package moves out of any other group
/// it was in; packages left out of the list leave the group.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SavePackageGroupCommand : ICommand<int>, IOrganizationRequest
{
    /// <summary>
    /// Most members of a group.
    /// </summary>
    public const int MaxMembers = 500;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Group to change; null adds one.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Member packages.
    /// </summary>
    public List<int> TrackedPackageIds { get; set; } = [];
}
