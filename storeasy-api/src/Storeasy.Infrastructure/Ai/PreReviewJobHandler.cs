using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Feedback;
using Storeasy.Domain.Monetization;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;
using System.Globalization;

namespace Storeasy.Infrastructure.Ai;

/// <summary>
/// The <see cref="StoreJobKind.PreReview"/> job: runs one AI pre-review. Steps on the activity hub:
/// <list type="number">
/// <item><c>submission.read</c>: reads the store record live (content and readiness check for the App Store and Google
/// Play, the current content for the Microsoft Store) and storeasy's records (saved answers, in-app products, past
/// rejections) into a <see cref="PreReviewSnapshot"/>;</item>
/// <item><c>screenshots.load</c>: downloads and scales down the chosen screenshots;</item>
/// <item><c>ai.review</c>: asks <see cref="IPreReviewer"/> with the organization's key and saves the report, the usage and the
/// cost estimate.</item>
/// </list>
/// Every failure is saved on the pre-review with its message key before the job fails, so the history shows why. Nothing is
/// written to a store. The audit entry keeps the model, the verdict, the counts, the tokens and the cost; no app content and
/// never the key.
/// </summary>
/// <param name="preReviewRepository"></param>
/// <param name="settingRepository"></param>
/// <param name="answerRepository"></param>
/// <param name="productRepository"></param>
/// <param name="rejectionRepository"></param>
/// <param name="msProductRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
/// <param name="microsoftStoreClient"></param>
/// <param name="secretProtector"></param>
/// <param name="rulesCatalog"></param>
/// <param name="imageLoader"></param>
/// <param name="preReviewer"></param>
/// <param name="auditTrail"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class PreReviewJobHandler(IStoreasyRepositoryBase<PreReview> preReviewRepository,
                                        IStoreasyRepositoryBase<OrganizationAiSetting> settingRepository,
                                        IStoreasyRepositoryBase<SavedAnswer> answerRepository,
                                        IStoreasyRepositoryBase<StoreProduct> productRepository,
                                        IStoreasyRepositoryBase<RejectionCase> rejectionRepository,
                                        IStoreasyRepositoryBase<MsProduct> msProductRepository,
                                        IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                        IStoreContentService contentService,
                                        IReadinessService readinessService,
                                        IMicrosoftStoreClient microsoftStoreClient,
                                        ISecretProtector secretProtector,
                                        IRulesCatalog rulesCatalog,
                                        IPreReviewImageLoader imageLoader,
                                        IPreReviewer preReviewer,
                                        IAuditTrail auditTrail,
                                        AiOptions options,
                                        TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<PreReview> _preReviewRepository = preReviewRepository;
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = settingRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = answerRepository;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<RejectionCase> _rejectionRepository = rejectionRepository;
    private readonly IStoreasyRepositoryBase<MsProduct> _msProductRepository = msProductRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly IPreReviewImageLoader _imageLoader = imageLoader;
    private readonly IPreReviewer _preReviewer = preReviewer;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly AiOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.PreReview;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<PreReviewPayload>() ?? throw new StoreJobException("The job has no pre-review.", transient: false);

        var preReview = await _preReviewRepository.GetFirstOrDefaultAsync(r => r.Id == payload.PreReviewId && r.OrganizationId == context.OrganizationId,
                                                                          projection: r => new PreReview { Id = r.Id, StoreAppId = r.StoreAppId, Store = r.Store, Model = r.Model, Language = r.Language, Status = r.Status },
                                                                          cancellationToken: cancellationToken)
                        ?? throw new StoreJobException("The pre-review was deleted.", transient: false);

        if (preReview.Status is PreReviewStatus.Succeeded or PreReviewStatus.Failed)
            throw new StoreJobException("The pre-review has already finished.", transient: false);

        await _preReviewRepository.ExecuteUpdateAsync(preReview.Id, new SetPropertyBuilder<PreReview>().SetPropertyValue(r => r.Status, PreReviewStatus.Running), cancellationToken);

        try
        {
            return await RunAsync(context, preReview, cancellationToken);
        }
        catch (PreReviewFailure failure)
        {
            await FailAsync(preReview.Id, failure.ErrorKey, failure.Usage, preReview.Model, cancellationToken);

            throw new StoreJobException($"Pre-review failed: {failure.ErrorKey}.", transient: false);
        }
        catch (OperationCanceledException)
        {
            // A member cancelled the job, or the host stops: the pre-review is not left running.
            await FailAsync(preReview.Id, MessageKey.PreReviewCancelled, AiUsage.None, preReview.Model, CancellationToken.None);

            throw;
        }
        catch (Exception)
        {
            await FailAsync(preReview.Id, MessageKey.PreReviewFailed, AiUsage.None, preReview.Model, CancellationToken.None);

            throw;
        }
    }

    private async Task<StoreJobOutcome> RunAsync(IStoreJobContext context, PreReview preReview, CancellationToken cancellationToken)
    {
        // 1. The submission.
        var readStep = await context.BeginStepAsync("submission.read", StepDirection.Inbound, cancellationToken: cancellationToken);

        var setting = await _settingRepository.OpenAiSettingAsync(_secretProtector, context.OrganizationId, cancellationToken)
                      ?? throw new PreReviewFailure(MessageKey.AiKeyNotSet);

        var app = await _contentService.GetStoreAppAsync(context.OrganizationId, preReview.StoreAppId, cancellationToken)
                  ?? throw new PreReviewFailure(MessageKey.StoreAppNotFound);

        var input = await GatherAsync(app, cancellationToken);
        var snapshot = PreReviewSnapshotBuilder.Build(input);

        await context.EndStepAsync(readStep, StoreJobStepStatus.Succeeded, $"{snapshot.Text.Length} characters, {snapshot.ImageCandidates.Count} screenshots found.", cancellationToken);

        // 2. The screenshots.
        var imageStep = await context.BeginStepAsync("screenshots.load", StepDirection.Inbound, total: Math.Min(snapshot.ImageCandidates.Count, _options.PreReviewMaxImages), cancellationToken: cancellationToken);
        var images = await _imageLoader.LoadAsync(snapshot.ImageCandidates, _options.PreReviewMaxImages, cancellationToken);

        await context.EndStepAsync(imageStep, StoreJobStepStatus.Succeeded, $"{images.Count} screenshots loaded.", cancellationToken);

        // 3. The review.
        var reviewStep = await context.BeginStepAsync("ai.review", StepDirection.Outbound, preReview.Model, cancellationToken: cancellationToken);
        var result = await _preReviewer.ReviewAsync(new PreReviewRequest(setting.ApiKey,
                                                                         preReview.Model,
                                                                         app.Store,
                                                                         preReview.Language,
                                                                         PreReviewPrompt.RulesBlock(_rulesCatalog.Current, app.Store),
                                                                         snapshot,
                                                                         images),
                                                    cancellationToken);

        if (result.Outcome != PreReviewOutcome.Reviewed)
        {
            await context.EndStepAsync(reviewStep, StoreJobStepStatus.Failed, result.Outcome.ToString(), cancellationToken);

            throw new PreReviewFailure(FailureKey(result.Outcome), result.Usage);
        }

        var report = result.Report;
        var usage = result.Usage;
        var cost = AiModelCatalog.EstimateCostUsd(preReview.Model, usage.InputTokens, usage.OutputTokens, usage.CacheReadInputTokens, usage.CacheCreationInputTokens);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _preReviewRepository.ExecuteUpdateAsync(preReview.Id,
                                                      new SetPropertyBuilder<PreReview>().SetPropertyValue(r => r.Status, PreReviewStatus.Succeeded)
                                                                                         .SetPropertyValue(r => r.VersionString, Short(snapshot.VersionString, PreReview.VersionMaxLength))
                                                                                         .SetPropertyValue(r => r.SnapshotHash, snapshot.Hash)
                                                                                         .SetPropertyValue(r => r.Verdict, report.Verdict)
                                                                                         .SetPropertyValue(r => r.ApprovalLikelihood, report.ApprovalLikelihood)
                                                                                         .SetPropertyValue(r => r.Summary, report.Summary)
                                                                                         .SetPropertyValue(r => r.Findings, report.Findings)
                                                                                         .SetPropertyValue(r => r.RuntimeChecks, report.RuntimeChecks)
                                                                                         .SetPropertyValue(r => r.PositiveNotes, report.PositiveNotes)
                                                                                         .SetPropertyValue(r => r.ImageCount, images.Count)
                                                                                         .SetPropertyValue(r => r.InputTokens, usage.InputTokens)
                                                                                         .SetPropertyValue(r => r.OutputTokens, usage.OutputTokens)
                                                                                         .SetPropertyValue(r => r.CacheReadInputTokens, usage.CacheReadInputTokens)
                                                                                         .SetPropertyValue(r => r.CacheCreationInputTokens, usage.CacheCreationInputTokens)
                                                                                         .SetPropertyValue(r => r.EstimatedCostUsd, cost)
                                                                                         .SetPropertyValue(r => r.CompletedDate, now),
                                                      cancellationToken);

        var blockers = report.Findings.Count(f => f.Severity == PreReviewSeverity.Blocker);
        var majors = report.Findings.Count(f => f.Severity == PreReviewSeverity.Major);
        var minors = report.Findings.Count(f => f.Severity == PreReviewSeverity.Minor);
        var summary = $"{report.Verdict}, {report.ApprovalLikelihood}% likely to pass; {blockers} blockers, {majors} major, {minors} minor.";

        await context.EndStepAsync(reviewStep, StoreJobStepStatus.Succeeded, summary, cancellationToken);

        await _auditTrail.WriteAsync(context.OrganizationId,
                                     AuditAction.PreReviewCompleted,
                                     preReview.Id.ToString(CultureInfo.InvariantCulture),
                                     $"AI pre-review of {app.AppName} ({app.Store}): {summary}",
                                     app.Store,
                                     details: new
                                     {
                                         preReview.Model,
                                         AnsweredBy = result.Model,
                                         report.Verdict,
                                         report.ApprovalLikelihood,
                                         Blockers = blockers,
                                         Majors = majors,
                                         Minors = minors,
                                         Images = images.Count,
                                         usage.InputTokens,
                                         usage.OutputTokens,
                                         usage.CacheReadInputTokens,
                                         usage.CacheCreationInputTokens,
                                         EstimatedCostUsd = cost,
                                     },
                                     cancellationToken: cancellationToken);

        return new StoreJobOutcome(summary, new { PreReviewId = preReview.Id, report.Verdict, report.ApprovalLikelihood });
    }

    private async Task<PreReviewInput> GatherAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var input = new PreReviewInput { App = app };

        if (app.Store == StoreKind.Microsoft)
        {
            input.MsContent = await ReadMicrosoftAsync(app, cancellationToken);
        }
        else
        {
            var read = await _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);

            if (!read.IsSuccess)
                throw new PreReviewFailure(read.ErrorKey);

            input.Content = read.Snapshot;

            var readiness = await _readinessService.CheckAsync(app, cancellationToken);

            input.Readiness = readiness.IsSuccess ? readiness.Data : null;
        }

        var answers = await _answerRepository.GetAllAsync(a => a.AppId == app.AppId && a.OrganizationId == app.OrganizationId,
                                                          projection: a => new SavedAnswer { Kind = a.Kind, Answers = a.Answers },
                                                          cancellationToken: cancellationToken);

        input.Answers = answers.GroupBy(a => a.Kind).ToDictionary(g => g.Key, g => g.First().Answers);

        var products = await _productRepository.GetAllAsync(p => p.StoreAppId == app.StoreAppId && p.OrganizationId == app.OrganizationId,
                                                            projection: p => new StoreProduct
                                                            {
                                                                Kind = p.Kind,
                                                                ProductId = p.ProductId,
                                                                ReferenceName = p.ReferenceName,
                                                                State = p.State,
                                                                Duration = p.Duration,
                                                                Localizations = p.Localizations,
                                                                ReviewNote = p.ReviewNote,
                                                                ReviewScreenshot = p.ReviewScreenshot,
                                                                Prices = p.Prices,
                                                            },
                                                            cancellationToken: cancellationToken);

        input.Products = [.. products.OrderBy(p => p.Kind).ThenBy(p => p.ProductId, StringComparer.Ordinal).Select(p => new PreReviewProduct(p.Kind,
                                                                                                                                          p.ProductId,
                                                                                                                                          p.ReferenceName,
                                                                                                                                          p.State,
                                                                                                                                          p.Duration,
                                                                                                                                          p.Localizations ?? [],
                                                                                                                                          !string.IsNullOrWhiteSpace(p.ReviewNote),
                                                                                                                                          p.ReviewScreenshot != null,
                                                                                                                                          p.Prices?.Count ?? 0))];

        var rejections = await _rejectionRepository.GetAllAsync(c => c.StoreAppId == app.StoreAppId && c.OrganizationId == app.OrganizationId,
                                                                projection: c => new RejectionCase { OpenedDate = c.OpenedDate, VersionString = c.VersionString, Kind = c.Kind, State = c.State, Guidelines = c.Guidelines, RawText = c.RawText },
                                                                cancellationToken: cancellationToken);

        input.Rejections = [.. rejections.OrderByDescending(c => c.OpenedDate)
                                         .Take(PreReviewSnapshotBuilder.MaxRejections)
                                         .Select(c => new PreReviewRejection(c.OpenedDate, c.VersionString, c.Kind, c.State, c.Guidelines ?? [], c.RawText))];

        return input;
    }

    private async Task<Application.Dtos.MsStoreDtos.MsContentDto> ReadMicrosoftAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var product = await _msProductRepository.GetMsProductAsync(app.OrganizationId, app.StoreAppId, cancellationToken)
                      ?? throw new PreReviewFailure(MessageKey.StoreAppNotFound);

        var secret = await _connectionRepository.OpenMsSecretAsync(_secretProtector, app.OrganizationId, product.ConnectionId, cancellationToken)
                     ?? throw new PreReviewFailure(MessageKey.StoreConnectionNotFound);

        try
        {
            return await MsContentReader.ReadAsync(_microsoftStoreClient,
                                                   secret,
                                                   new MsContentReader.Request
                                                   {
                                                       Kind = product.Kind,
                                                       ProductId = product.ProductId,
                                                       Target = product.Kind == MsProductKind.MsiExe ? MsSubmissionTarget.Installer : MsSubmissionTarget.App,
                                                   },
                                                   cancellationToken);
        }
        catch (StoreCallException)
        {
            throw new PreReviewFailure(MessageKey.MsStoreCallFailed);
        }
    }

    private async Task FailAsync(long preReviewId, string errorKey, AiUsage usage, string model, CancellationToken cancellationToken)
    {
        usage ??= AiUsage.None;

        await _preReviewRepository.ExecuteUpdateAsync(preReviewId,
                                                      new SetPropertyBuilder<PreReview>().SetPropertyValue(r => r.Status, PreReviewStatus.Failed)
                                                                                         .SetPropertyValue(r => r.ErrorKey, Short(errorKey, PreReview.ErrorKeyMaxLength))
                                                                                         .SetPropertyValue(r => r.InputTokens, usage.InputTokens)
                                                                                         .SetPropertyValue(r => r.OutputTokens, usage.OutputTokens)
                                                                                         .SetPropertyValue(r => r.CacheReadInputTokens, usage.CacheReadInputTokens)
                                                                                         .SetPropertyValue(r => r.CacheCreationInputTokens, usage.CacheCreationInputTokens)
                                                                                         .SetPropertyValue(r => r.EstimatedCostUsd, AiModelCatalog.EstimateCostUsd(model, usage.InputTokens, usage.OutputTokens, usage.CacheReadInputTokens, usage.CacheCreationInputTokens))
                                                                                         .SetPropertyValue(r => r.CompletedDate, _timeProvider.GetUtcNow().UtcDateTime),
                                                      cancellationToken);
    }

    /// <summary>
    /// Message key of a failed pre-review call.
    /// </summary>
    /// <param name="outcome"></param>
    /// <returns></returns>
    public static string FailureKey(PreReviewOutcome outcome) => outcome switch
    {
        PreReviewOutcome.KeyRejected => MessageKey.AiKeyRejected,
        PreReviewOutcome.RateLimited => MessageKey.AiRateLimited,
        PreReviewOutcome.Unavailable => MessageKey.AiUnavailable,
        PreReviewOutcome.Refused => MessageKey.PreReviewRefused,
        _ => MessageKey.PreReviewFailed,
    };

    private static string Short(string value, int max) => value == null || value.Length <= max ? value : value[..max];

    /// <summary>
    /// A failure with its message key, saved on the pre-review before the job fails.
    /// </summary>
    /// <param name="errorKey"></param>
    /// <param name="usage"></param>
    private sealed class PreReviewFailure(string errorKey, AiUsage usage = null) : Exception(errorKey)
    {
        /// <summary>
        /// Message key of the failure.
        /// </summary>
        public string ErrorKey { get; } = errorKey ?? MessageKey.PreReviewFailed;

        /// <summary>
        /// Tokens billed before it failed.
        /// </summary>
        public AiUsage Usage { get; } = usage ?? AiUsage.None;
    }
}
