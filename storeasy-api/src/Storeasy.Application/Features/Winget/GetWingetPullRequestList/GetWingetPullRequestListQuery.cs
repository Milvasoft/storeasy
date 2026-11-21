using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.WingetDtos;

namespace Storeasy.Application.Features.Winget.GetWingetPullRequestList;

/// <summary>
/// Lists the pull requests of a winget package with their review state (validation, feedback, approval, merged =
/// published), newest first. Database only; the sync refreshes them.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetWingetPullRequestListQuery : IQuery<List<WingetPullRequestDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// winget store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
