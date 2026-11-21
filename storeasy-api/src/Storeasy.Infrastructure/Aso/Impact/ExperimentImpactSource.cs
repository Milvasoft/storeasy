using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.Aso.Impact;

/// <summary>
/// Impact events of store listing experiments (App Store product page optimization, Google Play store listing
/// experiments): the day an experiment started getting traffic and the day it ended, from the experiment records. A test
/// splits the default page's traffic, so it confounds other changes in its windows. Applying the winner is a change plan
/// (<see cref="ChangePlanImpactSource"/>, <see cref="ImpactEventKind.ExperimentWinnerApplied"/>).
/// </summary>
/// <param name="dbContext"></param>
public sealed class ExperimentImpactSource(StoreasyDbContext dbContext) : IImpactEventSource
{
    private readonly StoreasyDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<List<ImpactEvent>> GetEventsAsync(ImpactEventQuery query, CancellationToken cancellationToken)
    {
        var from = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var kinds = ImpactEventRules.ExperimentKinds.ToList();

        var experimentsQuery = _dbContext.AsoExperiments.AsNoTracking()
                                                        .Where(e => e.OrganizationId == query.OrganizationId
                                                                    && kinds.Contains(e.Kind)
                                                                    && !e.StoreApp.App.IsDeleted
                                                                    && ((e.StartDate >= from && e.StartDate < to) || (e.EndDate >= from && e.EndDate < to)));

        if (query.AppId.HasValue)
            experimentsQuery = experimentsQuery.Where(e => e.AppId == query.AppId.Value);

        if (query.StoreAppId.HasValue)
            experimentsQuery = experimentsQuery.Where(e => e.StoreAppId == query.StoreAppId.Value);

        var experiments = await experimentsQuery.Select(e => new { e.Id, e.AppId, e.StoreAppId, e.Store, e.Name, e.ExternalId, e.StartDate, e.EndDate })
                                                .ToListAsync(cancellationToken);

        var events = new List<ImpactEvent>();

        foreach (var experiment in experiments)
        {
            foreach (var (at, start) in new[] { (experiment.StartDate, true), (experiment.EndDate, false) })
            {
                if (!at.HasValue)
                    continue;

                var day = ImpactEventRules.Day(at.Value);

                if (day < query.From || day > query.To)
                    continue;

                events.Add(new ImpactEvent
                {
                    Id = ImpactEventRules.ExperimentEventId(experiment.Id, start),
                    Kind = start ? ImpactEventKind.ExperimentStarted : ImpactEventKind.ExperimentEnded,
                    Date = day,
                    At = at,
                    DateSource = ImpactDateSource.Experiment,
                    OrganizationId = query.OrganizationId,
                    AppId = experiment.AppId,
                    StoreAppId = experiment.StoreAppId,
                    Store = experiment.Store,
                    Title = experiment.Name,
                    ExternalId = experiment.ExternalId,
                });
            }
        }

        return events;
    }
}
