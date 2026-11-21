using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Reads of experiment records shared by the experiment requests.
/// </summary>
public static class AsoExperimentExtensions
{
    /// <summary>
    /// The record <paramref name="id"/> of the organization, or null.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="organizationId"></param>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<AsoExperiment> FindAsync(this IStoreasyRepositoryBase<AsoExperiment> repository, int organizationId, int id, CancellationToken cancellationToken)
        => repository.GetFirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId, cancellationToken: cancellationToken);

    /// <summary>
    /// The detail of the record <paramref name="id"/> of the organization, or null.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="planRepository"></param>
    /// <param name="storeAppRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="id"></param>
    /// <param name="now"></param>
    /// <param name="localizer"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<AsoExperimentDto> DetailAsync(this IStoreasyRepositoryBase<AsoExperiment> repository,
                                                          IStoreasyRepositoryBase<ChangePlan> planRepository,
                                                          IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                                          int organizationId,
                                                          int id,
                                                          DateTime now,
                                                          IMilvaLocalizer localizer,
                                                          CancellationToken cancellationToken)
    {
        var record = await repository.FindAsync(organizationId, id, cancellationToken);

        if (record == null)
            return null;

        var planIds = record.PlanIds ?? [];
        var plans = planIds.Count == 0
            ? []
            : await planRepository.GetAllAsync(p => p.OrganizationId == organizationId && planIds.Contains(p.Id),
                                               projection: p => new ChangePlan
                                               {
                                                   Id = p.Id,
                                                   Title = p.Title,
                                                   Status = p.Status,
                                                   ExpiryDate = p.ExpiryDate,
                                                   CreationDate = p.CreationDate,
                                                   DecisionDate = p.DecisionDate,
                                                   AppliedDate = p.AppliedDate,
                                               },
                                               cancellationToken: cancellationToken);

        var externalAppId = await storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == record.StoreAppId && s.OrganizationId == organizationId,
                                                                            projection: s => s.ExternalId,
                                                                            cancellationToken: cancellationToken);

        return AsoExperimentViews.Detail(record, externalAppId, [.. plans.OrderBy(p => p.Id)], now, localizer);
    }
}
