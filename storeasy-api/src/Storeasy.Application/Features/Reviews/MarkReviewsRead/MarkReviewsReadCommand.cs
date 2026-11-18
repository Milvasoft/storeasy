using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.MarkReviewsRead;

/// <summary>
/// Marks reviews as read or unread for the caller (the mark is personal). Returns how many reviews were marked.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record MarkReviewsReadCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Reviews.
    /// </summary>
    public List<long> ReviewIds { get; set; } = [];

    /// <summary>
    /// Read (true) or unread (false).
    /// </summary>
    public bool IsRead { get; set; } = true;
}
