using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.Aso.Impact;

/// <summary>
/// Impact events from applied change plans that change the store page (listing texts, screenshots, icon, previews, price,
/// category) and Google Play production releases. The effect day prefers the store's live day: an App Store change
/// written to a version counts from the day the version went live (and waits while it is not live); a Microsoft Store
/// change from the day the next app submission was published. Rules in <see cref="ImpactEventRules"/>.
/// </summary>
/// <param name="dbContext"></param>
public sealed class ChangePlanImpactSource(StoreasyDbContext dbContext) : IImpactEventSource
{
    private readonly StoreasyDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<List<ImpactEvent>> GetEventsAsync(ImpactEventQuery query, CancellationToken cancellationToken)
    {
        var appliedFrom = query.From.AddDays(-ImpactEventRules.ReleaseLookbackDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var appliedTo = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var kinds = ImpactEventRules.PlanKinds.ToList();

        var plansQuery = _dbContext.ChangePlans.AsNoTracking()
                                               .Where(p => p.OrganizationId == query.OrganizationId
                                                           && p.Status == ChangePlanStatus.Applied
                                                           && p.AppliedDate != null
                                                           && p.AppliedDate >= appliedFrom
                                                           && p.AppliedDate < appliedTo
                                                           && kinds.Contains(p.Kind)
                                                           && !p.StoreApp.App.IsDeleted);

        if (query.AppId.HasValue)
            plansQuery = plansQuery.Where(p => p.AppId == query.AppId.Value);

        if (query.StoreAppId.HasValue)
            plansQuery = plansQuery.Where(p => p.StoreAppId == query.StoreAppId.Value);

        var plans = await plansQuery.Select(p => new PlanRow(p.Id, p.AppId, p.StoreAppId, p.Store, p.Kind, p.Title, p.VersionId, p.VersionString, p.Items, p.AppliedDate.Value))
                                    .ToListAsync(cancellationToken);

        if (plans.Count == 0)
            return [];

        var appleVersions = await AppleVersionsAsync(plans, cancellationToken);
        var msPublished = await MsPublishedAsync(plans, cancellationToken);
        var events = new List<ImpactEvent>();

        foreach (var plan in plans)
        {
            var kind = ImpactEventRules.KindOf(plan.Kind, plan.Items);

            if (!kind.HasValue)
                continue;

            var (at, source, releaseKey) = Effect(plan, appleVersions, msPublished);
            var day = ImpactEventRules.Day(at);

            if (day < query.From || day > query.To)
                continue;

            events.Add(new ImpactEvent
            {
                Id = ImpactEventRules.PlanPrefix + plan.Id,
                Kind = kind.Value,
                Date = day,
                At = at,
                DateSource = source,
                OrganizationId = query.OrganizationId,
                AppId = plan.AppId,
                StoreAppId = plan.StoreAppId,
                Store = plan.Store,
                Title = plan.Title,
                Locales = ImpactEventRules.Locales(plan.Items),
                Fields = ImpactEventRules.Fields(plan.Items),
                PlanId = plan.Id,
                VersionString = plan.VersionString,
                ReleaseKey = releaseKey,
            });
        }

        return events;
    }

    private static (DateTime At, ImpactDateSource Source, string ReleaseKey) Effect(PlanRow plan,
                                                                                   Dictionary<(int, string), (string State, DateTime Changed)> appleVersions,
                                                                                   Dictionary<int, List<(long Id, DateTime Published)>> msPublished)
    {
        if (plan.Store == StoreKind.Apple && !string.IsNullOrEmpty(plan.VersionId))
        {
            var known = appleVersions.TryGetValue((plan.StoreAppId, plan.VersionId), out var version);
            var (at, source) = ImpactEventRules.AppleEffect(plan.AppliedDate, known ? version.State : null, known ? version.Changed : null);

            return (at, source, source == ImpactDateSource.StoreLive ? $"{plan.StoreAppId}:{plan.VersionId}" : null);
        }

        if (plan.Store == StoreKind.Microsoft && plan.Kind == ChangePlanKind.MsChange)
        {
            // A Microsoft Store change is live once a later app submission is published.
            var published = msPublished.TryGetValue(plan.StoreAppId, out var list) ? list.Where(s => s.Published >= plan.AppliedDate).OrderBy(s => s.Published).FirstOrDefault() : default;

            return published.Id != 0 ? (published.Published, ImpactDateSource.StoreLive, $"ms:{published.Id}") : (plan.AppliedDate, ImpactDateSource.AwaitingRelease, null);
        }

        return (plan.AppliedDate, ImpactDateSource.Applied, null);
    }

    private async Task<Dictionary<(int, string), (string State, DateTime Changed)>> AppleVersionsAsync(List<PlanRow> plans, CancellationToken cancellationToken)
    {
        var storeAppIds = plans.Where(p => p.Store == StoreKind.Apple && !string.IsNullOrEmpty(p.VersionId)).Select(p => p.StoreAppId).Distinct().ToList();

        if (storeAppIds.Count == 0)
            return [];

        var versionIds = plans.Where(p => p.Store == StoreKind.Apple && !string.IsNullOrEmpty(p.VersionId)).Select(p => p.VersionId).Distinct().ToList();
        var states = await _dbContext.AppVersionStates.AsNoTracking()
                                                      .Where(s => storeAppIds.Contains(s.StoreAppId) && versionIds.Contains(s.ExternalVersionId))
                                                      .Select(s => new { s.StoreAppId, s.ExternalVersionId, s.State, s.ChangedDate })
                                                      .ToListAsync(cancellationToken);

        return states.GroupBy(s => (s.StoreAppId, s.ExternalVersionId)).ToDictionary(g => g.Key, g => (g.First().State, g.First().ChangedDate));
    }

    private async Task<Dictionary<int, List<(long Id, DateTime Published)>>> MsPublishedAsync(List<PlanRow> plans, CancellationToken cancellationToken)
    {
        var storeAppIds = plans.Where(p => p.Store == StoreKind.Microsoft && p.Kind == ChangePlanKind.MsChange).Select(p => p.StoreAppId).Distinct().ToList();

        if (storeAppIds.Count == 0)
            return [];

        var submissions = await _dbContext.MsSubmissions.AsNoTracking()
                                                        .Where(s => storeAppIds.Contains(s.StoreAppId) && s.Target == MsSubmissionTarget.App)
                                                        .Select(s => new { s.Id, s.StoreAppId, s.StatusHistory })
                                                        .ToListAsync(cancellationToken);

        return submissions.Select(s => (s.Id, s.StoreAppId, Published: ImpactEventRules.MsPublishedDate(s.StatusHistory)))
                          .Where(s => s.Published.HasValue)
                          .GroupBy(s => s.StoreAppId)
                          .ToDictionary(g => g.Key, g => g.Select(s => (s.Id, s.Published.Value)).ToList());
    }

    private sealed record PlanRow(long Id, int AppId, int StoreAppId, StoreKind Store, ChangePlanKind Kind, string Title, string VersionId, string VersionString, List<PlanItem> Items, DateTime AppliedDate);
}
