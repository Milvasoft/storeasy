using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Feedback;

/// <summary>
/// A reusable review reply of an organization, one text per language. <c>{ad}</c> / <c>{name}</c>, <c>{uygulama}</c> /
/// <c>{app}</c> and <c>{sürüm}</c> / <c>{version}</c> are filled from the review when the reply is picked.
/// </summary>
[Table(TableNames.SavedReplies)]
[Index(nameof(OrganizationId))]
public class SavedReply : FullAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Longest text of one variant.
    /// </summary>
    public const int BodyMaxLength = 6000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Text per language.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<LocalizedText> Variants { get; set; } = [];

    /// <summary>
    /// Apps it is suggested for; empty means every app.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<int> AppIds { get; set; } = [];

    /// <summary>
    /// How many replies used it.
    /// </summary>
    public int UseCount { get; set; }
}
