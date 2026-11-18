using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Reviews.GetSavedReplyList;

/// <summary>
/// The organization's saved replies. With <see cref="Locale"/> the replies that have that language come first; with
/// <see cref="AppId"/> only the replies suggested for that app.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSavedReplyListQuery : IQuery<List<SavedReplyDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Language of the review being answered.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// App of the review being answered.
    /// </summary>
    public int? AppId { get; set; }
}
