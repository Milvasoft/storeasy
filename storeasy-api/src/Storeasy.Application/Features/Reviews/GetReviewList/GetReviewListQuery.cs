using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Reviews.GetReviewList;

/// <summary>
/// The organization's review inbox (every app, App Store and Google Play), newest first.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetReviewListQuery : ListRequest, IListRequestQuery<ReviewListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Only this store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Lowest rating.
    /// </summary>
    public int? MinRating { get; set; }

    /// <summary>
    /// Highest rating.
    /// </summary>
    public int? MaxRating { get; set; }

    /// <summary>
    /// Only this language (prefix match: <c>en</c> matches <c>en-US</c>).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Only replied (true) or unreplied (false) reviews.
    /// </summary>
    public bool? HasReply { get; set; }

    /// <summary>
    /// Only reviews the caller read (true) or did not read (false).
    /// </summary>
    public bool? IsRead { get; set; }

    /// <summary>
    /// Text in the review body, title or author.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Only reviews written after this UTC instant.
    /// </summary>
    public DateTime? Since { get; set; }
}
