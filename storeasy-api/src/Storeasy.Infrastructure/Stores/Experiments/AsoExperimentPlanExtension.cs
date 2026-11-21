using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;
using Storeasy.Domain.JsonModels;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Experiments;

/// <summary>
/// Change plans of App Store experiments and custom product pages (<see cref="ChangePlanKind.AsoExperiment"/>): on
/// approval the experiments are read again for the fingerprint (applying a winner reads the app version's media); the
/// apply job writes through <see cref="IAsoExperimentGateway"/>, saves the store ids it got onto the record, then reads the
/// store again so the record shows the new state.
/// </summary>
/// <param name="gateways"></param>
/// <param name="experimentStore"></param>
/// <param name="contentService"></param>
/// <param name="experimentRepository"></param>
/// <param name="timeProvider"></param>
public sealed class AsoExperimentPlanExtension(IEnumerable<IAsoExperimentGateway> gateways,
                                               IAsoExperimentStore experimentStore,
                                               IStoreContentService contentService,
                                               IStoreasyRepositoryBase<AsoExperiment> experimentRepository,
                                               TimeProvider timeProvider) : IChangePlanExtension
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IEnumerable<IAsoExperimentGateway> _gateways = gateways;
    private readonly IAsoExperimentStore _experimentStore = experimentStore;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = experimentRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public bool Handles(ChangePlanKind kind) => kind == ChangePlanKind.AsoExperiment;

    /// <inheritdoc/>
    public async Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken)
    {
        if (baseKeys.All(k => !k.StartsWith(AsoExperimentContent.Prefix, StringComparison.Ordinal)))
            return await _contentService.ReadAsync(app, ContentParts.Screenshots | ContentParts.Previews, null, cancellationToken);

        var read = await _experimentStore.ReadAsync(app, cancellationToken);

        return read.IsSuccess ? new StoreContentRead(AsoExperimentContent.AsContent(read.Read)) : new StoreContentRead(null, read.ErrorKey, read.ErrorDetail);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == request.Store) ?? throw new StoreJobException($"{request.Store} has no experiments API.", transient: false);
        var changes = JsonSerializer.Deserialize<AsoExperimentChangeSet>(request.Changes, _json) ?? throw new StoreJobException("The plan has no experiment changes.", transient: false);
        var result = await gateway.ApplyAsync(request.Target, changes, context, cancellationToken);

        var record = await _experimentRepository.GetFirstOrDefaultAsync(r => r.Id == changes.ExperimentId && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (record == null)
            return result.Lines;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        record.ExternalId = result.ExternalId ?? record.ExternalId;
        record.ExternalVersionId = result.ExternalVersionId ?? record.ExternalVersionId;
        record.Url = result.Url ?? record.Url;

        if (record.Status == AsoExperimentStatus.Draft && record.ExternalId != null)
            record.Status = AsoExperimentStatus.Prepared;

        foreach (var (key, id) in result.TreatmentIds)
        {
            var variant = record.Variants.FirstOrDefault(v => v.Key == key);

            if (variant != null)
                variant.ExternalId = id;
        }

        if (changes.Operation == AsoExperimentOperation.Submit && !changes.WithVersion)
            record.SubmittedDate = now;

        // The store's answer after the write (state, dates); a failed read only leaves the record as written.
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);
        var read = app == null ? null : await _experimentStore.ReadAsync(app, cancellationToken);

        if (read?.IsSuccess == true)
            AsoExperimentContent.Merge(record, read.Read);

        await AsoExperimentStoreSync.SaveStateAsync(_experimentRepository, record, now, cancellationToken);

        return result.Lines;
    }

    /// <inheritdoc/>
    public AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item) => item.Field switch
    {
        "review" => AuditAction.AsoExperimentSubmitted,
        "started" => item.After == "true" ? AuditAction.AsoExperimentStarted : AuditAction.AsoExperimentStopped,
        _ when item.Group == ContentGroups.Media => AuditAction.AsoExperimentWinnerApplied,
        _ => AuditAction.AsoExperimentWritten,
    };
}
