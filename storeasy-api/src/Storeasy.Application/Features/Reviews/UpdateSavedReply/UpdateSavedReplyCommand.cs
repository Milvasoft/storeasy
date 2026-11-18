using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.UpdateSavedReply;

/// <summary>
/// Changes a saved reply. Null fields stay as they are; the change applies to replies picked from now on.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record UpdateSavedReplyCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Saved reply.
    /// </summary>
    public int SavedReplyId { get; set; }

    /// <summary>
    /// New name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// New variants (the whole list).
    /// </summary>
    public List<LocalizedText> Variants { get; set; }

    /// <summary>
    /// New apps (the whole list; empty: every app).
    /// </summary>
    public List<int> AppIds { get; set; }
}
