using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Mockups.DeleteMockupTemplate;

/// <summary>
/// Deletes a mockup template (soft delete; exported images and their jobs stay). Returns the template id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record DeleteMockupTemplateCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }
}
