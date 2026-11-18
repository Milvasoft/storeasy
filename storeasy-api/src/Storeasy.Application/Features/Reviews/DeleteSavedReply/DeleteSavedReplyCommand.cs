using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.DeleteSavedReply;

/// <summary>
/// Removes a saved reply.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record DeleteSavedReplyCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Saved reply.
    /// </summary>
    public int SavedReplyId { get; set; }
}
