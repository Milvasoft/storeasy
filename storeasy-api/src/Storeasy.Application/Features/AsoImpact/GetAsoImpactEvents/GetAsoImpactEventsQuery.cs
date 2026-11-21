using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpactEvents;

/// <summary>
/// The changes of one app that may have moved its store page metrics, newest first: applied change plans of the store
/// page (listing texts, screenshots, icon, previews, price, category) and versions that went live, each with the day it
/// took effect in the store. The range is <see cref="From"/> to <see cref="To"/>, or the last <see cref="Days"/> days
/// ending today (180 by default), at most 400 days. Read from storeasy's rows only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoImpactEventsQuery : IQuery<List<AsoImpactEventDto>>, IOrganizationRequest
{
    /// <summary>
    /// Default number of days.
    /// </summary>
    public const int DefaultDays = 180;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Only this store record of the app.
    /// </summary>
    public int? StoreAppId { get; set; }

    /// <summary>
    /// Only this store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// First effect day; null for <see cref="Days"/> days before <see cref="To"/>.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Last effect day; null for today (UTC).
    /// </summary>
    public DateOnly? To { get; set; }

    /// <summary>
    /// Range length in days when <see cref="From"/> is null (default 180).
    /// </summary>
    public int? Days { get; set; }
}
