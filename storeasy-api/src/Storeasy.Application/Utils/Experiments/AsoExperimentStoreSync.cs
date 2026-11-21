using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Utils.Experiments;

/// <summary>
/// Saves what a store read says about the experiment records of one store record: the known records get their state,
/// dates, address and treatment ids; experiments and custom product pages made in App Store Connect get a record of their
/// own (so the impact report sees them too). Records the store no longer has are left as they are.
/// </summary>
public static class AsoExperimentStoreSync
{
    /// <summary>
    /// Saves <paramref name="read"/> into the records of <paramref name="app"/>. Returns the updated and added counts.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="app"></param>
    /// <param name="read"></param>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<(int Updated, int Added)> SaveAsync(IStoreasyRepositoryBase<AsoExperiment> repository,
                                                                StoreAppContext app,
                                                                StoreAsoRead read,
                                                                DateTime now,
                                                                CancellationToken cancellationToken)
    {
        var records = await repository.GetAllAsync(r => r.OrganizationId == app.OrganizationId && r.StoreAppId == app.StoreAppId, cancellationToken: cancellationToken);
        var updated = 0;

        foreach (var record in records)
        {
            if (!AsoExperimentContent.Merge(record, read))
                continue;

            if (record.Status == AsoExperimentStatus.InReview && record.SubmittedDate == null)
                record.SubmittedDate = now;

            await SaveStateAsync(repository, record, now, cancellationToken);
            updated++;
        }

        var known = records.Where(r => r.ExternalId != null).Select(r => r.ExternalId).ToHashSet(StringComparer.Ordinal);
        var found = AsoExperimentContent.Discover(app, read, known);

        foreach (var record in found)
        {
            record.SyncedDate = now;
            record.CreationDate = now;
            record.CreatorUserName = "store";
        }

        if (found.Count > 0)
            await repository.AddRangeAsync(found, cancellationToken);

        return (updated, found.Count);
    }

    /// <summary>
    /// Writes the store state fields of <paramref name="record"/>.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="record"></param>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task SaveStateAsync(IStoreasyRepositoryBase<AsoExperiment> repository, AsoExperiment record, DateTime now, CancellationToken cancellationToken)
    {
        record.SyncedDate = now;

        return repository.ExecuteUpdateAsync(record.Id,
                                             new SetPropertyBuilder<AsoExperiment>().SetPropertyValue(r => r.Status, record.Status)
                                                                                     .SetPropertyValue(r => r.StoreState, record.StoreState)
                                                                                     .SetPropertyValue(r => r.ExternalId, record.ExternalId)
                                                                                     .SetPropertyValue(r => r.ExternalVersionId, record.ExternalVersionId)
                                                                                     .SetPropertyValue(r => r.TrafficProportion, record.TrafficProportion)
                                                                                     .SetPropertyValue(r => r.ReviewRequired, record.ReviewRequired)
                                                                                     .SetPropertyValue(r => r.StartDate, record.StartDate)
                                                                                     .SetPropertyValue(r => r.EndDate, record.EndDate)
                                                                                     .SetPropertyValue(r => r.Url, record.Url)
                                                                                     .SetPropertyValue(r => r.Variants, record.Variants)
                                                                                     .SetPropertyValue(r => r.SubmittedDate, record.SubmittedDate)
                                                                                     .SetPropertyValue(r => r.SyncedDate, record.SyncedDate),
                                             cancellationToken);
    }
}
