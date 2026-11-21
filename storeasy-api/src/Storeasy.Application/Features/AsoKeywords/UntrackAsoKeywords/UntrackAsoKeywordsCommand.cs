using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.AsoKeywords.UntrackAsoKeywords;

/// <summary>
/// Stops the daily rank checks of tracked keywords. Their rank history is kept (reports read it) and tracking the same
/// keyword again continues it. Returns how many were stopped; 404 AsoTrackedKeywordNotFound when none of the ids is an
/// active keyword of the organization.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UntrackAsoKeywordsCommand : ICommand<int>, IOrganizationRequest
{
    /// <summary>
    /// Most ids per call.
    /// </summary>
    public const int MaxIds = 200;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracking ids (from the tracked list).
    /// </summary>
    public List<int> Ids { get; set; } = [];
}
