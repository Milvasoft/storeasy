using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Feedback;

/// <summary>
/// A customer review of an app in a store with its developer reply. Google Play's API returns only recent reviews that
/// have text, so every review read is kept: older ones stay here after the store stops listing them.
/// </summary>
[Table(TableNames.StoreReviews)]
[Index(nameof(StoreAppId), nameof(ExternalReviewId), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(ReviewDate), IsDescending = [false, true])]
public class StoreReview : BaseEntity<long>
{
    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 200;

    /// <summary>
    /// Column length of <see cref="Body"/> and <see cref="ReplyBody"/>.
    /// </summary>
    public const int BodyMaxLength = 10000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the review.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ExternalReviewId { get; set; }

    /// <summary>
    /// Reviewer's nickname.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string AuthorName { get; set; }

    /// <summary>
    /// Stars, 1 to 5.
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Title (App Store only).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Title { get; set; }

    /// <summary>
    /// Review text.
    /// </summary>
    [MaxLength(BodyMaxLength)]
    public string Body { get; set; }

    /// <summary>
    /// Language of the review, if known.
    /// </summary>
    [MaxLength(20)]
    public string Locale { get; set; }

    /// <summary>
    /// Storefront or country, if known.
    /// </summary>
    [MaxLength(20)]
    public string Territory { get; set; }

    /// <summary>
    /// App version reviewed, if known.
    /// </summary>
    [MaxLength(50)]
    public string AppVersion { get; set; }

    /// <summary>
    /// Device, if known (Play).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Device { get; set; }

    /// <summary>
    /// UTC instant the review was written or last edited.
    /// </summary>
    public DateTime ReviewDate { get; set; }

    /// <summary>
    /// Developer reply text.
    /// </summary>
    [MaxLength(BodyMaxLength)]
    public string ReplyBody { get; set; }

    /// <summary>
    /// Reply state.
    /// </summary>
    public ReviewReplyState ReplyState { get; set; }

    /// <summary>
    /// The store's id of the reply (App Store customer review response), when it has one.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ReplyExternalId { get; set; }

    /// <summary>
    /// UTC instant of the reply.
    /// </summary>
    public DateTime? ReplyDate { get; set; }

    /// <summary>
    /// Who replied through storeasy.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ReplyByUserName { get; set; }

    /// <summary>
    /// Redacted reason of the last failed reply.
    /// </summary>
    [MaxLength(500)]
    public string ReplyError { get; set; }

    /// <summary>
    /// Job of the last reply.
    /// </summary>
    public long? ReplyJobId { get; set; }

    /// <summary>
    /// Unsent draft (written by a member or by Claude through MCP; never sent without a person).
    /// </summary>
    [MaxLength(BodyMaxLength)]
    public string DraftBody { get; set; }

    /// <summary>
    /// UTC instant the review was first read from the store.
    /// </summary>
    public DateTime FetchedDate { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    [ForeignKey(nameof(AppId))]
    public virtual Apps.App App { get; set; }

    /// <summary>
    /// Read marks of the members.
    /// </summary>
    public virtual List<ReviewRead> Reads { get; set; }
}
