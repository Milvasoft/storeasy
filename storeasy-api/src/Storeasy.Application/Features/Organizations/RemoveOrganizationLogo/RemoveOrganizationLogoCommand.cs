using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.RemoveOrganizationLogo;

/// <summary>
/// Removes the organization's logo; it shows its initial again. Removing when there is no logo succeeds and changes nothing.
/// </summary>
[OrgAuth(OrgPermission.ManageOrganization)]
public record RemoveOrganizationLogoCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
