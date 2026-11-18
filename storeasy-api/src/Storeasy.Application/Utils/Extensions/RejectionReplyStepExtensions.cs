using Microsoft.EntityFrameworkCore;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// The Resolution Center reply of a rejection is an assisted step (no API sends it): the step of the catalog rule
/// <see cref="ReplyStepKey"/> of the app is reopened when an App Store rejection case opens and completed when the
/// reply is marked as sent, from the rejection screen or from the assisted step screen.
/// </summary>
public static class RejectionReplyStepExtensions
{
    /// <summary>
    /// Catalog rule (and assisted step key) of the Resolution Center reply.
    /// </summary>
    public const string ReplyStepKey = "apple.review.resolution-center-reply";

    /// <summary>
    /// Opens (or reopens) the reply step of every app in <paramref name="appIds"/>. Returns nothing; a concurrent insert
    /// of the same step is ignored.
    /// </summary>
    /// <param name="stepRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="appIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task OpenReplyStepsAsync(this IStoreasyRepositoryBase<AssistedStep> stepRepository, int organizationId, IReadOnlyCollection<int> appIds, CancellationToken cancellationToken)
    {
        if (appIds.Count == 0)
            return;

        var ids = appIds.Distinct().ToList();

        await stepRepository.ExecuteUpdateAsync(s => s.OrganizationId == organizationId && ids.Contains(s.AppId) && s.Store == StoreKind.Apple && s.Key == ReplyStepKey,
                                                new SetPropertyBuilder<AssistedStep>().SetPropertyValue(s => s.Status, AssistedStepStatus.Todo)
                                                                                      .SetPropertyValue(s => s.CompletedDate, (DateTime?)null)
                                                                                      .SetPropertyValue(s => s.CompletedByUserName, null)
                                                                                      .SetPropertyValue(s => s.CompletedChannel, (ActionChannel?)null),
                                                cancellationToken);

        var existing = (await stepRepository.GetAllAsync(s => s.OrganizationId == organizationId && ids.Contains(s.AppId) && s.Store == StoreKind.Apple && s.Key == ReplyStepKey,
                                                         projection: s => new AssistedStep { AppId = s.AppId },
                                                         cancellationToken: cancellationToken)).Select(s => s.AppId).ToHashSet();

        var missing = ids.Where(id => !existing.Contains(id)).Select(id => new AssistedStep { OrganizationId = organizationId, AppId = id, Store = StoreKind.Apple, Key = ReplyStepKey, Status = AssistedStepStatus.Todo }).ToList();

        if (missing.Count == 0)
            return;

        try
        {
            await stepRepository.AddRangeAsync(missing, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request opened the same step.
        }
    }

    /// <summary>
    /// Completes the reply step of an app.
    /// </summary>
    /// <param name="stepRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="userName"></param>
    /// <param name="channel"></param>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<int> CompleteReplyStepAsync(this IStoreasyRepositoryBase<AssistedStep> stepRepository, int organizationId, int appId, string userName, ActionChannel channel, DateTime now, CancellationToken cancellationToken)
        => stepRepository.ExecuteUpdateAsync(s => s.OrganizationId == organizationId && s.AppId == appId && s.Store == StoreKind.Apple && s.Key == ReplyStepKey,
                                             new SetPropertyBuilder<AssistedStep>().SetPropertyValue(s => s.Status, AssistedStepStatus.Done)
                                                                                   .SetPropertyValue(s => s.CompletedDate, (DateTime?)now)
                                                                                   .SetPropertyValue(s => s.CompletedByUserName, userName)
                                                                                   .SetPropertyValue(s => s.CompletedChannel, (ActionChannel?)channel),
                                             cancellationToken);
}
