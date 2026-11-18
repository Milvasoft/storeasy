using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.StoreJobDtos;

namespace Storeasy.Application.Features.Mockups.ExportMockups;

/// <summary>
/// Queues a mockup export job (<see cref="StoreJobKind.MockupExport"/>) for the plan the same selection gives: every
/// selected class, locale and slot is rendered on the server, stored as assets and zipped; progress streams on the activity
/// hub (<c>job.step</c>, one <c>render.size</c> step per class). Nothing is written to a store: the job's manifest feeds
/// the screenshot upload plan. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record ExportMockupsCommand : ICommand<long>, IOrganizationRequest
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
    /// Sizes to render instead of the class defaults (refused sizes fall back to the default).
    /// </summary>
    public List<MockupExportTarget> Targets { get; set; }
}
