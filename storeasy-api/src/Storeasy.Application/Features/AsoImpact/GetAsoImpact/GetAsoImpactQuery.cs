using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpact;

/// <summary>
/// Before and after report of one change of an app: a window of <see cref="WindowDays"/> days before the effect day and
/// one of the same length starting <see cref="GapDays"/> days after it (the default skips the effect day and the next,
/// since stores apply changes with a delay). Impressions, page views and downloads as daily averages over matching
/// weekdays, conversion rate with a two proportion z-test ("not enough data" below 100 page views a window), tracked
/// keyword ranks, and the other changes inside the windows (confounding). Read from storeasy's rows only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoImpactQuery : IQuery<AsoImpactReportDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Event id from the event list (<c>plan-12</c>, <c>version-34</c>, <c>ms-56</c>).
    /// </summary>
    public string EventId { get; set; }

    /// <summary>
    /// Length of each window in days, 3 to 56 (default 14; 7, 14 and 28 are whole weeks).
    /// </summary>
    public int? WindowDays { get; set; }

    /// <summary>
    /// Days skipped from the effect day on, 0 to 14 (default 2: the effect day and the next).
    /// </summary>
    public int? GapDays { get; set; }

    /// <summary>
    /// Only this country (ISO 3166 code, e.g. <c>TR</c>); null for every country. Also limits the keywords compared.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Only this traffic source; null for every source.
    /// </summary>
    public TrafficSource? Source { get; set; }

    /// <summary>
    /// Whether to compare the tracked keywords' ranks too (default true).
    /// </summary>
    public bool? IncludeRanks { get; set; }
}
