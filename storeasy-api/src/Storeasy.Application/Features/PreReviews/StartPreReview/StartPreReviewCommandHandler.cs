using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.PreReviewDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;
using System.Globalization;

namespace Storeasy.Application.Features.PreReviews.StartPreReview;

/// <summary>
/// Handles <see cref="StartPreReviewCommand"/>: checks the store record, the key and that no pre-review of the record is
/// still running, saves a queued pre-review with the organization's model and the request's language, queues its job (one
/// attempt: every attempt costs the organization money) and writes the audit entry. No [Log]: the answer carries token
/// counts, which the log masking takes for secrets; the audit entry records the start.
/// </summary>
/// <param name="PreReviewRepository"></param>
/// <param name="SettingRepository"></param>
/// <param name="JobRepository"></param>
/// <param name="ContentService"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="Localizer"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
public record StartPreReviewCommandHandler(IStoreasyRepositoryBase<PreReview> PreReviewRepository,
                                           IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                           IStoreasyRepositoryBase<StoreJob> JobRepository,
                                           IStoreContentService ContentService,
                                           IStoreJobQueue StoreJobQueue,
                                           IAuditTrail AuditTrail,
                                           ICurrentUser CurrentUser,
                                           IMilvaLocalizer Localizer,
                                           AiOptions Options,
                                           TimeProvider TimeProvider) : IInterceptable, ICommandHandler<StartPreReviewCommand, PreReviewDto>
{
    /// <summary>
    /// Report language when the request has none.
    /// </summary>
    public const string DefaultLanguage = "tr-TR";

    private readonly IStoreasyRepositoryBase<PreReview> _preReviewRepository = PreReviewRepository;
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = JobRepository;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly AiOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<PreReviewDto>> Handle(StartPreReviewCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<PreReviewDto>.Error(null, MessageKey.StoreAppNotFound);

        var setting = await _settingRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId,
                                                                      projection: s => new OrganizationAiSetting { Id = s.Id, PreReviewModel = s.PreReviewModel },
                                                                      cancellationToken: cancellationToken);

        if (setting == null)
            return Response<PreReviewDto>.Error(null, MessageKey.AiKeyNotSet);

        var open = await _preReviewRepository.GetAllAsync(r => r.StoreAppId == app.StoreAppId
                                                               && r.OrganizationId == request.OrganizationId
                                                               && (r.Status == PreReviewStatus.Queued || r.Status == PreReviewStatus.Running),
                                                          projection: r => new PreReview { Id = r.Id, Status = r.Status, JobId = r.JobId },
                                                          cancellationToken: cancellationToken);

        await _jobRepository.ResolveInterruptedAsync(open, cancellationToken);

        if (open.Any(r => r.Status is PreReviewStatus.Queued or PreReviewStatus.Running))
            return Response<PreReviewDto>.Error(null, MessageKey.PreReviewInProgress);

        var language = CultureInfo.CurrentUICulture.Name;
        var preReview = new PreReview
        {
            OrganizationId = request.OrganizationId,
            AppId = app.AppId,
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            Status = PreReviewStatus.Queued,
            Model = _options.PreReviewModelFor(setting.PreReviewModel),
            Language = string.IsNullOrWhiteSpace(language) || language.Length > PreReview.LanguageMaxLength ? DefaultLanguage : language,
            CreationDate = _timeProvider.GetUtcNow().UtcDateTime,
            CreatorUserName = _currentUser.UserName,
        };

        await _preReviewRepository.AddAsync(preReview, cancellationToken);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.PreReview, new PreReviewPayload(preReview.Id), app.Store, app.AppId, MaxAttempts: 1)], cancellationToken);

        preReview.JobId = jobIds[0];

        await _preReviewRepository.ExecuteUpdateAsync(preReview.Id, new SetPropertyBuilder<PreReview>().SetPropertyValue(r => r.JobId, preReview.JobId), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.PreReviewStarted,
                                     preReview.Id.ToString(CultureInfo.InvariantCulture),
                                     $"AI pre-review of {app.AppName} ({app.Store}) queued as job {preReview.JobId} with {preReview.Model}.",
                                     app.Store,
                                     details: new { preReview.JobId, preReview.Model, preReview.Language },
                                     cancellationToken: cancellationToken);

        return Response<PreReviewDto>.Success(preReview.ToDto(_localizer));
    }
}
