using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpactRecent;

/// <summary>
/// The impact of the organization's recent changes ("Son değişikliklerin etkisi"): every live change of the last
/// <see cref="Days"/> days (default 60) of every app, newest first, at most <see cref="Limit"/>, each with page views,
/// downloads and conversion rate before and after (windows of <see cref="WindowDays"/> days, the default gap), the
/// conversion signal and whether another change confounds it. Keyword ranks are in the single report.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoImpactRecentQuery : IQuery<AsoImpactRecentDto>, IOrganizationRequest
{
    /// <summary>
    /// Default number of days.
    /// </summary>
    public const int DefaultDays = 60;

    /// <summary>
    /// Longest range.
    /// </summary>
    public const int MaxDays = 180;

    /// <summary>
    /// Default number of changes.
    /// </summary>
    public const int DefaultLimit = 20;

    /// <summary>
    /// Most changes in one call.
    /// </summary>
    public const int MaxLimit = 50;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Days up to today, 1 to 180 (default 60).
    /// </summary>
    public int? Days { get; set; }

    /// <summary>
    /// Length of each window in days, 3 to 56 (default 14).
    /// </summary>
    public int? WindowDays { get; set; }

    /// <summary>
    /// Only this store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Most changes, 1 to 50 (default 20).
    /// </summary>
    public int? Limit { get; set; }
}
