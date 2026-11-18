using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Studio;

/// <summary>
/// The raw screenshot of one slot of a <see cref="MockupTemplate"/> for one device family, optionally for one locale (a
/// locale's own screenshot wins over the shared one). Rows are replaced as a set when the template is saved.
/// </summary>
[Table(TableNames.MockupSlots)]
[Index(nameof(MockupTemplateId), nameof(Order))]
public class MockupSlot : BaseEntity<long>
{
    /// <summary>
    /// Owning template.
    /// </summary>
    public int MockupTemplateId { get; set; }

    /// <summary>
    /// Slot position, from 1.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Device family of the screenshot.
    /// </summary>
    public MockupDeviceFamily Family { get; set; }

    /// <summary>
    /// Locale the screenshot is for; null for every locale.
    /// </summary>
    [MaxLength(MockupTemplate.LocaleMaxLength)]
    public string Locale { get; set; }

    /// <summary>
    /// The uploaded screenshot (an asset of the template's organization).
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Navigation property of the template.
    /// </summary>
    [ForeignKey(nameof(MockupTemplateId))]
    public virtual MockupTemplate MockupTemplate { get; set; }
}
