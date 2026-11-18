using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.UpdateOrganization;

/// <summary>
/// Renames an organization.
/// </summary>
[OrgAuth(OrgPermission.ManageOrganization)]
public record UpdateOrganizationCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// New display name.
    /// </summary>
    public string Name { get; set; }
}
