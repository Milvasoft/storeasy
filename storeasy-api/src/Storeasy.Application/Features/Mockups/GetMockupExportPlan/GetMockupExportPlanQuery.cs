using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Application.Dtos.StoreJobDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupExportPlan;

/// <summary>
/// Plans an export of a template without writing anything: the classes of the app's stores with their requirement, the
/// default selection, refused sizes and the image count (docs 12.10).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupExportPlanQuery : IQuery<MockupExportPlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Locales to export; null for every locale of the template.
    /// </summary>
    public List<string> Locales { get; set; }

    /// <summary>
    /// Classes to export; null for the required and recommended classes.
    /// </summary>
    public List<string> ClassKeys { get; set; }

    /// <summary>
    /// Sizes to render instead of the class defaults (each is checked against the sizes the store accepts).
    /// </summary>
    public List<MockupExportTarget> Targets { get; set; }
}
