using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// AI settings of an organization: the Anthropic API key used by the AI features (review reply drafts, pre-reviews) and
/// the model each feature uses. The key is envelope encrypted at rest and never returned; only its last four characters
/// are kept in clear for display. An organization without a row has no key (and the default models).
/// </summary>
[Table(TableNames.OrganizationAiSettings)]
[Index(nameof(OrganizationId), IsUnique = true)]
public class OrganizationAiSetting : CreationAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="ApiKeyLastFour"/>.
    /// </summary>
    public const int LastFourLength = 4;

    /// <summary>
    /// Column length of <see cref="ApiKeySetByUserName"/>.
    /// </summary>
    public const int UserNameMaxLength = 200;

    /// <summary>
    /// Column length of the model ids.
    /// </summary>
    public const int ModelMaxLength = 64;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The Anthropic API key sealed with the data key.
    /// </summary>
    public byte[] EncryptedApiKey { get; set; }

    /// <summary>
    /// The data key sealed with the master key.
    /// </summary>
    public byte[] EncryptedDataKey { get; set; }

    /// <summary>
    /// Master key version of <see cref="EncryptedDataKey"/>.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>
    /// Last four characters of the key, shown so members can tell keys apart.
    /// </summary>
    [MaxLength(LastFourLength)]
    public string ApiKeyLastFour { get; set; }

    /// <summary>
    /// UTC instant the current key was saved.
    /// </summary>
    public DateTime ApiKeySetDate { get; set; }

    /// <summary>
    /// Who saved the current key.
    /// </summary>
    [MaxLength(UserNameMaxLength)]
    public string ApiKeySetByUserName { get; set; }

    /// <summary>
    /// Model of the review reply drafts; null for the configured default.
    /// </summary>
    [MaxLength(ModelMaxLength)]
    public string ReplyDraftModel { get; set; }

    /// <summary>
    /// Model of the AI pre-reviews; null for the configured default.
    /// </summary>
    [MaxLength(ModelMaxLength)]
    public string PreReviewModel { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
