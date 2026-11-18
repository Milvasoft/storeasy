using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.UpdateOrganization;

/// <summary>
/// Renames an organization and, when <see cref="Slug"/> is sent, changes its URL key (normalized like at creation, unique).
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

    /// <summary>
    /// New URL key (optional; null or empty keeps the current one).
    /// </summary>
    public string Slug { get; set; }
}
