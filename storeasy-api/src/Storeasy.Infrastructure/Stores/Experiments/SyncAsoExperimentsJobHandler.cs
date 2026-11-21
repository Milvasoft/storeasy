using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Experiments;
using Storeasy.Infrastructure.Stores.Content;

namespace Storeasy.Infrastructure.Stores.Experiments;

/// <summary>
/// The <see cref="StoreJobKind.SyncAsoExperiments"/> job: reads the experiments and custom product pages of an App Store
/// record and saves their state into storeasy's records (<see cref="AsoExperimentStoreSync"/>); ones made in App Store
/// Connect get a record. A store that cannot be reached fails the step transiently and the runner retries.
/// </summary>
/// <param name="contentService"></param>
/// <param name="experimentStore"></param>
/// <param name="experimentRepository"></param>
/// <param name="timeProvider"></param>
public sealed class SyncAsoExperimentsJobHandler(IStoreContentService contentService,
                                                 IAsoExperimentStore experimentStore,
                                                 IStoreasyRepositoryBase<AsoExperiment> experimentRepository,
                                                 TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IAsoExperimentStore _experimentStore = experimentStore;
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = experimentRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncAsoExperiments;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncAsoExperimentsPayload>() ?? throw new StoreJobException("The job has no store record.", transient: false);
        var app = await _contentService.GetStoreAppAsync(context.OrganizationId, payload.StoreAppId, cancellationToken) ?? throw new StoreJobException("The store record was removed.", transient: false);

        if (app.Store != StoreKind.Apple)
            throw new StoreJobException($"{app.Store} has no experiments API.", transient: false);

        StoreAsoRead read = null;

        await ContentSteps.RunAsync(context, "experiments.read", app.AppName, async () =>
        {
            var result = await _experimentStore.ReadAsync(app, cancellationToken);

            if (!result.IsSuccess)
                throw new StoreJobException(result.ErrorDetail ?? result.ErrorKey, transient: result.ErrorKey == MessageKey.StoreUnreachable);

            read = result.Read;

            return $"{read.Experiments.Count} experiments, {read.CustomPages.Count} custom product pages read.";
        }, cancellationToken, StepDirection.Inbound);

        var (updated, added) = await AsoExperimentStoreSync.SaveAsync(_experimentRepository, app, read, _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        return new StoreJobOutcome($"{updated} experiment records updated, {added} added for store record {app.StoreAppId}.", new { app.StoreAppId, Updated = updated, Added = added });
    }
}
