using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.SyncReviews;

/// <summary>
/// Reads new reviews now (one job per App Store and Google Play record of the app, or of every app). They are also read
/// every 30 minutes. Returns the job ids.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record SyncReviewsCommand : ICommand<List<long>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app.
    /// </summary>
    public int? AppId { get; set; }
}
