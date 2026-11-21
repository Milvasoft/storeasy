using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.Aso.Impact;

/// <summary>
/// Impact events of versions that went live: App Store versions whose last known state is ready for sale (the day they
/// reached it) and Microsoft Store app submissions that were published (the first day storeasy saw them published).
/// Google Play production releases are change plans (<see cref="ChangePlanImpactSource"/>). An App Store version that was
/// replaced before storeasy saw it live has no known live day and is left out.
/// </summary>
/// <param name="dbContext"></param>
public sealed class ReleaseImpactSource(StoreasyDbContext dbContext) : IImpactEventSource
{
    private readonly StoreasyDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<List<ImpactEvent>> GetEventsAsync(ImpactEventQuery query, CancellationToken cancellationToken)
    {
        var from = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var events = new List<ImpactEvent>();

        var versionsQuery = _dbContext.AppVersionStates.AsNoTracking()
                                                       .Where(s => s.OrganizationId == query.OrganizationId
                                                                   && s.Store == StoreKind.Apple
                                                                   && (s.State == "READY_FOR_SALE" || s.State == "READY_FOR_DISTRIBUTION")
                                                                   && s.ChangedDate >= from
                                                                   && s.ChangedDate < to
                                                                   && _dbContext.Apps.Any(a => a.Id == s.AppId && !a.IsDeleted));

        if (query.AppId.HasValue)
            versionsQuery = versionsQuery.Where(s => s.AppId == query.AppId.Value);

        if (query.StoreAppId.HasValue)
            versionsQuery = versionsQuery.Where(s => s.StoreAppId == query.StoreAppId.Value);

        var versions = await versionsQuery.Select(s => new { s.Id, s.AppId, s.StoreAppId, s.ExternalVersionId, s.VersionString, s.ChangedDate }).ToListAsync(cancellationToken);

        events.AddRange(versions.Select(v => new ImpactEvent
        {
            Id = ImpactEventRules.VersionPrefix + v.Id,
            Kind = ImpactEventKind.VersionReleased,
            Date = ImpactEventRules.Day(v.ChangedDate),
            At = v.ChangedDate,
            DateSource = ImpactDateSource.StoreLive,
            OrganizationId = query.OrganizationId,
            AppId = v.AppId,
            StoreAppId = v.StoreAppId,
            Store = StoreKind.Apple,
            Title = $"Version {v.VersionString} released",
            VersionString = v.VersionString,
            ReleaseKey = $"{v.StoreAppId}:{v.ExternalVersionId}",
            ExternalId = v.ExternalVersionId,
        }));

        var submissionsQuery = _dbContext.MsSubmissions.AsNoTracking()
                                                       .Where(s => s.OrganizationId == query.OrganizationId
                                                                   && s.Target == MsSubmissionTarget.App
                                                                   && !s.StoreApp.App.IsDeleted);

        if (query.AppId.HasValue)
            submissionsQuery = submissionsQuery.Where(s => s.StoreApp.AppId == query.AppId.Value);

        if (query.StoreAppId.HasValue)
            submissionsQuery = submissionsQuery.Where(s => s.StoreAppId == query.StoreAppId.Value);

        var submissions = await submissionsQuery.Select(s => new { s.Id, s.StoreApp.AppId, s.StoreAppId, s.ExternalId, s.PackageVersion, s.StatusHistory }).ToListAsync(cancellationToken);

        foreach (var submission in submissions)
        {
            var published = ImpactEventRules.MsPublishedDate(submission.StatusHistory);

            if (!published.HasValue)
                continue;

            var day = ImpactEventRules.Day(published.Value);

            if (day < query.From || day > query.To)
                continue;

            events.Add(new ImpactEvent
            {
                Id = ImpactEventRules.MsSubmissionPrefix + submission.Id,
                Kind = ImpactEventKind.VersionReleased,
                Date = day,
                At = published,
                DateSource = ImpactDateSource.StoreLive,
                OrganizationId = query.OrganizationId,
                AppId = submission.AppId,
                StoreAppId = submission.StoreAppId,
                Store = StoreKind.Microsoft,
                Title = string.IsNullOrEmpty(submission.PackageVersion) ? "Submission published" : $"Submission {submission.PackageVersion} published",
                VersionString = submission.PackageVersion,
                ReleaseKey = $"ms:{submission.Id}",
                ExternalId = submission.ExternalId,
            });
        }

        return events;
    }
}
