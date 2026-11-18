using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Studio;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// A mockup template with its design, slots and captions (the editor's document).
/// </summary>
public class MockupTemplateDetailDto : MockupTemplateListDto
{
    /// <summary>
    /// The design (schema in storeasy-api/docs/studio.md).
    /// </summary>
    public MockupSpec Spec { get; set; }

    /// <summary>
    /// Raw screenshots of the slots.
    /// </summary>
    public List<MockupSlotDto> Slots { get; set; }

    /// <summary>
    /// Captions per locale and slot.
    /// </summary>
    public List<MockupCaptionDto> Captions { get; set; }

    /// <summary>
    /// Projection from a template row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static new Expression<Func<MockupTemplate, MockupTemplateDetailDto>> Projection { get; } = t => new MockupTemplateDetailDto
    {
        Id = t.Id,
        AppId = t.AppId,
        AppName = t.App.Name,
        Name = t.Name,
        PrimaryLocale = t.PrimaryLocale,
        Locales = t.Locales,
        SlotCount = t.SlotCount,
        StarterKey = t.StarterKey,
        Version = t.Version,
        CreationDate = t.CreationDate,
        LastModificationDate = t.LastModificationDate,
        LastModifierUserName = t.LastModifierUserName,
        LastExportJobId = t.LastExportJobId,
        LastExportDate = t.LastExportDate,
        LastExportImageCount = t.LastExportImageCount,
        Spec = t.Spec,
        Slots = t.Slots.OrderBy(s => s.Order).ThenBy(s => s.Family).ThenBy(s => s.Locale).Select(s => new MockupSlotDto
        {
            Order = s.Order,
            Family = s.Family,
            Locale = s.Locale,
            AssetId = s.AssetId,
        }).ToList(),
        Captions = t.Captions.OrderBy(c => c.Locale).ThenBy(c => c.SlotOrder).Select(c => new MockupCaptionDto
        {
            Locale = c.Locale,
            SlotOrder = c.SlotOrder,
            Title = c.Title,
            Subtitle = c.Subtitle,
        }).ToList(),
    };
}
