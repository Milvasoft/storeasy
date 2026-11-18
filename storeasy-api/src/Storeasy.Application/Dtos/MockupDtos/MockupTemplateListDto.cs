using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Studio;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// A mockup template in the studio list.
/// </summary>
public class MockupTemplateListDto
{
    /// <summary>
    /// Template id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// App of the template.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Name of the app.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Template name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Primary locale.
    /// </summary>
    public string PrimaryLocale { get; set; }

    /// <summary>
    /// Locales of the template.
    /// </summary>
    public List<string> Locales { get; set; }

    /// <summary>
    /// Number of slots.
    /// </summary>
    public int SlotCount { get; set; }

    /// <summary>
    /// Built in starter it was created from.
    /// </summary>
    public string StarterKey { get; set; }

    /// <summary>
    /// Concurrency version (send it back with an update).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// UTC instant of the creation.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant of the last change.
    /// </summary>
    public DateTime? LastModificationDate { get; set; }

    /// <summary>
    /// User name of the last editor.
    /// </summary>
    public string LastModifierUserName { get; set; }

    /// <summary>
    /// Last export job that produced images.
    /// </summary>
    public long? LastExportJobId { get; set; }

    /// <summary>
    /// UTC instant the last export finished.
    /// </summary>
    public DateTime? LastExportDate { get; set; }

    /// <summary>
    /// Images of the last export.
    /// </summary>
    public int? LastExportImageCount { get; set; }

    /// <summary>
    /// Projection from a template row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MockupTemplate, MockupTemplateListDto>> Projection { get; } = t => new MockupTemplateListDto
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
    };
}
