using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.SaveMockupCaptions;

/// <summary>
/// Saves caption cells of a template (only the changed ones need to be sent): a cell with an empty title and subtitle
/// removes its row. Answers the captions that overflow their line limit in the narrowest class the app exports, measured
/// with the template's fonts.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SaveMockupCaptionsCommand : ICommand<MockupCaptionsSavedDto>, IOrganizationRequest
{
    /// <summary>
    /// Most cells in one save.
    /// </summary>
    public const int MaxItems = 500;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Cells to write.
    /// </summary>
    public List<MockupCaptionDto> Items { get; set; } = [];
}
