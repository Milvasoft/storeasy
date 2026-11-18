using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Globalization;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IReadinessService"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="answerRepository"></param>
/// <param name="stepRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="rulesCatalog"></param>
/// <param name="localizer"></param>
/// <param name="timeProvider"></param>
public sealed class ReadinessService(IStoreContentService contentService,
                                    IStoreasyRepositoryBase<SavedAnswer> answerRepository,
                                    IStoreasyRepositoryBase<AssistedStep> stepRepository,
                                    IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                    IRulesCatalog rulesCatalog,
                                    IMilvaLocalizer localizer,
                                    TimeProvider timeProvider) : IReadinessService
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = answerRepository;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = stepRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly IMilvaLocalizer _localizer = localizer;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<ReadinessReportDto>> CheckAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);

        if (!read.IsSuccess)
        {
            return read.ErrorDetail == null
                ? Response<ReadinessReportDto>.Error(null, read.ErrorKey)
                : Response<ReadinessReportDto>.Error(null, new ResponseMessage(read.ErrorKey, read.ErrorDetail));
        }

        var answers = (await _answerRepository.GetAllAsync(a => a.AppId == app.AppId && a.OrganizationId == app.OrganizationId,
                                                           projection: a => new SavedAnswer { Kind = a.Kind },
                                                           cancellationToken: cancellationToken)).Select(a => a.Kind).ToHashSet();

        var steps = await GetStepsAsync(app.OrganizationId, app.AppId, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var catalog = _rulesCatalog.Current;

        var items = ReadinessEvaluator.Evaluate(new ReadinessInput(app,
                                                                   read.Snapshot,
                                                                   _contentService.Subject(app, read.Snapshot),
                                                                   catalog,
                                                                   answers,
                                                                   steps.GetValueOrDefault((app.AppId, app.Store)) ?? [],
                                                                   DateOnly.FromDateTime(now),
                                                                   CultureInfo.CurrentUICulture.Name,
                                                                   _localizer));

        var report = new ReadinessReportDto
        {
            AppId = app.AppId,
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            VersionId = read.Snapshot.VersionId,
            VersionString = read.Snapshot.VersionString,
            CatalogVersion = catalog.Version,
            CheckedDate = now,
            Ok = items.Count(i => i.Status == ReadinessStatus.Ok),
            Missing = items.Count(i => i.Status == ReadinessStatus.Missing),
            Warning = items.Count(i => i.Status == ReadinessStatus.Warning),
            Assisted = items.Count(i => i.Status == ReadinessStatus.Assisted),
            Unknown = items.Count(i => i.Status == ReadinessStatus.Unknown),
            Items = items,
        };

        report.CanSubmit = report.Missing == 0;

        var summary = new ReadinessSummary
        {
            CheckedDate = now,
            CatalogVersion = catalog.Version,
            VersionString = report.VersionString,
            Ok = report.Ok,
            Missing = report.Missing,
            Warning = report.Warning,
            Assisted = report.Assisted,
            Unknown = report.Unknown,
            MissingKeys = [.. items.Where(i => i.Status == ReadinessStatus.Missing).Select(i => i.Key)],
        };

        await _storeAppRepository.ExecuteUpdateAsync(app.StoreAppId, new SetPropertyBuilder<StoreApp>().SetPropertyValue(s => s.LastReadiness, summary), cancellationToken);

        return Response<ReadinessReportDto>.Success(report);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<(int AppId, StoreKind Store), Dictionary<string, (int Id, AssistedStepStatus Status)>>> GetStepsAsync(int organizationId, int? appId, CancellationToken cancellationToken)
    {
        var rows = await _stepRepository.GetAllAsync(s => s.OrganizationId == organizationId && (appId == null || s.AppId == appId),
                                                     projection: s => new AssistedStep { Id = s.Id, AppId = s.AppId, Store = s.Store, Key = s.Key, Status = s.Status },
                                                     cancellationToken: cancellationToken);

        return rows.GroupBy(r => (r.AppId, r.Store)).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Key, r => (r.Id, r.Status)));
    }
}
