using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.RenderMockupPreview;

/// <summary>
/// Renders small previews of a template's slots in one locale and screenshot class, from the stored design or an
/// unsaved one sent with the request. Same renderer and layout as the export, scaled down; nothing is stored.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record RenderMockupPreviewQuery : IQuery<MockupPreviewDto>, IOrganizationRequest
{
    /// <summary>
    /// Most slots in one preview.
    /// </summary>
    public const int MaxSlots = 4;

    /// <summary>
    /// Narrowest preview width.
    /// </summary>
    public const int MinWidth = 120;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Locale; null for the primary locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Screenshot class; null for the design's canvas class.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Slots to draw; null for the first three.
    /// </summary>
    public List<int> SlotOrders { get; set; }

    /// <summary>
    /// Unsaved design to draw instead of the stored one.
    /// </summary>
    public MockupSpec Spec { get; set; }

    /// <summary>
    /// Width of each preview in pixels; null for 400 (at most the configured maximum).
    /// </summary>
    public int? MaxWidth { get; set; }
}
