using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Studio;

/// <summary>
/// Title and subtitle of one slot of a <see cref="MockupTemplate"/> in one locale. An empty caption is drawn with the
/// template's primary locale.
/// </summary>
[Table(TableNames.MockupCaptions)]
[Index(nameof(MockupTemplateId), nameof(Locale), nameof(SlotOrder), IsUnique = true)]
public class MockupCaption : BaseEntity<long>
{
    /// <summary>
    /// Column length of <see cref="Title"/>.
    /// </summary>
    public const int TitleMaxLength = 200;

    /// <summary>
    /// Column length of <see cref="Subtitle"/>.
    /// </summary>
    public const int SubtitleMaxLength = 300;

    /// <summary>
    /// Owning template.
    /// </summary>
    public int MockupTemplateId { get; set; }

    /// <summary>
    /// Locale tag.
    /// </summary>
    [MaxLength(MockupTemplate.LocaleMaxLength)]
    public string Locale { get; set; }

    /// <summary>
    /// Slot position, from 1.
    /// </summary>
    public int SlotOrder { get; set; }

    /// <summary>
    /// Title (a new line is a manual line break).
    /// </summary>
    [MaxLength(TitleMaxLength)]
    public string Title { get; set; }

    /// <summary>
    /// Subtitle.
    /// </summary>
    [MaxLength(SubtitleMaxLength)]
    public string Subtitle { get; set; }

    /// <summary>
    /// Navigation property of the template.
    /// </summary>
    [ForeignKey(nameof(MockupTemplateId))]
    public virtual MockupTemplate MockupTemplate { get; set; }
}
