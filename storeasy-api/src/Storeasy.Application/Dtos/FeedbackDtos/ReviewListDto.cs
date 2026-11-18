using Storeasy.Domain.Feedback;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// A review in the organization's inbox.
/// </summary>
public class ReviewListDto
{
    /// <summary>Review id.</summary>
    public long Id { get; set; }

    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>App name.</summary>
    public string AppName { get; set; }

    /// <summary>Store.</summary>
    public StoreKind Store { get; set; }

    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>The store's id of the review.</summary>
    public string ExternalReviewId { get; set; }

    /// <summary>Stars.</summary>
    public int Rating { get; set; }

    /// <summary>Title.</summary>
    public string Title { get; set; }

    /// <summary>Text.</summary>
    public string Body { get; set; }

    /// <summary>Reviewer.</summary>
    public string AuthorName { get; set; }

    /// <summary>Language.</summary>
    public string Locale { get; set; }

    /// <summary>Storefront or country.</summary>
    public string Territory { get; set; }

    /// <summary>Reviewed version.</summary>
    public string AppVersion { get; set; }

    /// <summary>Device.</summary>
    public string Device { get; set; }

    /// <summary>UTC instant written.</summary>
    public DateTime ReviewDate { get; set; }

    /// <summary>Whether the caller read it.</summary>
    public bool IsRead { get; set; }

    /// <summary>Reply text.</summary>
    public string ReplyBody { get; set; }

    /// <summary>Reply state.</summary>
    public ReviewReplyState ReplyState { get; set; }

    /// <summary>UTC instant replied.</summary>
    public DateTime? ReplyDate { get; set; }

    /// <summary>Who replied through storeasy.</summary>
    public string ReplyByUserName { get; set; }

    /// <summary>Reason of the last failed reply.</summary>
    public string ReplyError { get; set; }

    /// <summary>Job of the last reply.</summary>
    public long? ReplyJobId { get; set; }

    /// <summary>Longest reply the store accepts.</summary>
    public int ReplyMaxLength { get; set; }

    /// <summary>Unsent draft.</summary>
    public string DraftBody { get; set; }

    /// <summary>
    /// Projection from the entity (read mark and limit are filled in memory).
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<StoreReview, ReviewListDto>> Projection { get; } = r => new ReviewListDto
    {
        Id = r.Id,
        AppId = r.AppId,
        Store = r.Store,
        StoreAppId = r.StoreAppId,
        ExternalReviewId = r.ExternalReviewId,
        Rating = r.Rating,
        Title = r.Title,
        Body = r.Body,
        AuthorName = r.AuthorName,
        Locale = r.Locale,
        Territory = r.Territory,
        AppVersion = r.AppVersion,
        Device = r.Device,
        ReviewDate = r.ReviewDate,
        ReplyBody = r.ReplyBody,
        ReplyState = r.ReplyState,
        ReplyDate = r.ReplyDate,
        ReplyByUserName = r.ReplyByUserName,
        ReplyError = r.ReplyError,
        ReplyJobId = r.ReplyJobId,
        DraftBody = r.DraftBody,
    };
}
