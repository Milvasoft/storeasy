using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.MsStore;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// Row writes of the Microsoft write jobs: storeasy's record of a submission it wrote, the pending submission of its owner,
/// and the marks of the Partner Center rules.
/// </summary>
public static class MsRowWriter
{
    /// <summary>
    /// Saves what storeasy knows of a submission after writing or reading it: status (with history), pending flag and,
    /// when storeasy wrote the content, its fingerprint.
    /// </summary>
    /// <param name="submissionRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="target"></param>
    /// <param name="submissionId"></param>
    /// <param name="change">Applies the new values to the row.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MsSubmission> UpsertSubmissionAsync(this IStoreasyRepositoryBase<MsSubmission> submissionRepository,
                                                                 int organizationId,
                                                                 int storeAppId,
                                                                 MsSubmissionTarget target,
                                                                 string submissionId,
                                                                 Action<MsSubmission> change,
                                                                 CancellationToken cancellationToken)
    {
        var row = await submissionRepository.GetFirstOrDefaultAsync(s => s.StoreAppId == storeAppId && s.Target == target && s.ExternalId == submissionId, cancellationToken: cancellationToken)
                  ?? new MsSubmission { OrganizationId = organizationId, StoreAppId = storeAppId, Target = target, ExternalId = submissionId };

        change(row);

        if (row.Id == 0)
        {
            await submissionRepository.AddAsync(row, cancellationToken);
        }
        else
        {
            await submissionRepository.UpdateAsync(row,
                                                   cancellationToken,
                                                   s => s.Status,
                                                   s => s.StatusHistory,
                                                   s => s.StatusDetails,
                                                   s => s.IsPending,
                                                   s => s.CreatedByStoreasy,
                                                   s => s.ModifiedOutsideApi,
                                                   s => s.ContentHash,
                                                   s => s.FlightId,
                                                   s => s.AddonStoreId,
                                                   s => s.FriendlyName,
                                                   s => s.RolloutPercentage,
                                                   s => s.RolloutStatus,
                                                   s => s.PublishMode,
                                                   s => s.PublishDate,
                                                   s => s.SyncedDate);
        }

        return row;
    }

    /// <summary>
    /// Records that a submission storeasy created was changed in Partner Center (the API refused it or its content changed).
    /// </summary>
    /// <param name="submissionRepository"></param>
    /// <param name="storeAppId"></param>
    /// <param name="target"></param>
    /// <param name="submissionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<int> MarkModifiedOutsideApiAsync(this IStoreasyRepositoryBase<MsSubmission> submissionRepository, int storeAppId, MsSubmissionTarget target, string submissionId, CancellationToken cancellationToken)
        => submissionRepository.ExecuteUpdateAsync(s => s.StoreAppId == storeAppId && s.Target == target && s.ExternalId == submissionId && s.CreatedByStoreasy,
                                                   new SetPropertyBuilder<MsSubmission>().SetPropertyValue(s => s.ModifiedOutsideApi, true),
                                                   cancellationToken);

    /// <summary>
    /// Applies a status read to a row.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="status"></param>
    /// <param name="details"></param>
    /// <param name="now"></param>
    public static void Observe(MsSubmission row, MsSubmissionStatus status, MsStatusDetails details, DateTime now) => MsSubmissionRows.Observe(row, status, details, now);
}
