using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Studio;

/// <summary>
/// A mockup template of one app (docs/urun-plani.md 2.4): the design (<see cref="Spec"/>, the single source of preview and
/// export), its slots' raw screenshots (<see cref="Slots"/>) and its captions per locale (<see cref="Captions"/>).
/// <see cref="Version"/> guards concurrent edits: an update names the version it started from and loses with 409 when
/// someone saved in between.
/// </summary>
[Table(TableNames.MockupTemplates)]
[Index(nameof(OrganizationId), nameof(AppId))]
public class MockupTemplate : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Column length of a locale tag.
    /// </summary>
    public const int LocaleMaxLength = 20;

    /// <summary>
    /// Most slots (screenshots per locale) of a template; the stores accept at most 10 per class.
    /// </summary>
    public const int MaxSlotCount = 10;

    /// <summary>
    /// Most locales of a template.
    /// </summary>
    public const int MaxLocaleCount = 40;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App the template belongs to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Name, unique within the app.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Locale whose captions stand in for an empty caption of another locale.
    /// </summary>
    [MaxLength(LocaleMaxLength)]
    public string PrimaryLocale { get; set; }

    /// <summary>
    /// Locales the template is exported in (BCP 47 tags as the stores use them, e.g. <c>tr</c>, <c>en-US</c>).
    /// </summary>
    public List<string> Locales { get; set; }

    /// <summary>
    /// Number of slots (screenshots per locale and class).
    /// </summary>
    public int SlotCount { get; set; }

    /// <summary>
    /// The design.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public MockupSpec Spec { get; set; }

    /// <summary>
    /// Built in starter the template was created from, if any.
    /// </summary>
    [MaxLength(50)]
    public string StarterKey { get; set; }

    /// <summary>
    /// Concurrency version, increased by every update.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Last export job that produced images.
    /// </summary>
    public long? LastExportJobId { get; set; }

    /// <summary>
    /// UTC instant the last export finished.
    /// </summary>
    public DateTime? LastExportDate { get; set; }

    /// <summary>
    /// Images the last export produced.
    /// </summary>
    public int? LastExportImageCount { get; set; }

    /// <summary>
    /// Raw screenshots of the slots.
    /// </summary>
    public virtual List<MockupSlot> Slots { get; set; }

    /// <summary>
    /// Captions per locale and slot.
    /// </summary>
    public virtual List<MockupCaption> Captions { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    public virtual App App { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
