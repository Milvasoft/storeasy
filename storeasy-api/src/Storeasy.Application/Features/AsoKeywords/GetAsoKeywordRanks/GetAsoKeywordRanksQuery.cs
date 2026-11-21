using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordRanks;

/// <summary>
/// The daily rank history of one tracked keyword (active or untracked) for the last <see cref="Days"/> days (UTC), the
/// followed competitors' histories and the latest top ten results. A day without a check reads as not checked; a checked
/// day without a rank means the app was not in the results read.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoKeywordRanksQuery : IQuery<AsoKeywordRanksDto>, IOrganizationRequest
{
    /// <summary>
    /// Default number of days.
    /// </summary>
    public const int DefaultDays = 30;

    /// <summary>
    /// Longest history in one call.
    /// </summary>
    public const int MaxDays = 365;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracking id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Days up to today, 1 to 365 (default 30).
    /// </summary>
    public int Days { get; set; } = DefaultDays;
}
