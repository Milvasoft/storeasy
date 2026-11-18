using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Apps;

/// <summary>
/// One saved answer document of an app (docs/urun-plani.md 11.1): the answers to a store form that has no API (App Privacy,
/// content rating, target audience) or that storeasy writes from them (Data safety CSV, App Review details). One row per
/// app and <see cref="Kind"/>. <see cref="Answers"/> is the typed JSON document of the kind; the only secret (the demo
/// account password of <see cref="SavedAnswerKind.ReviewLogin"/>) is envelope encrypted in <see cref="EncryptedSecret"/>,
/// is never returned and never copied to another app.
/// </summary>
[Table(TableNames.SavedAnswers)]
[Index(nameof(AppId), nameof(Kind), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class SavedAnswer : AuditableEntity<int>
{
    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Which form the answers are for.
    /// </summary>
    public SavedAnswerKind Kind { get; set; }

    /// <summary>
    /// The answers as the typed JSON document of <see cref="Kind"/>.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Answers { get; set; }

    /// <summary>
    /// The secret sealed with the data key; null when there is none.
    /// </summary>
    public byte[] EncryptedSecret { get; set; }

    /// <summary>
    /// The data key sealed with the master key.
    /// </summary>
    public byte[] EncryptedDataKey { get; set; }

    /// <summary>
    /// Version of the master key that sealed <see cref="EncryptedDataKey"/>.
    /// </summary>
    public int? KeyVersion { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    [ForeignKey(nameof(AppId))]
    public virtual App App { get; set; }
}
