using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Aso;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.AsoKeywords.SuggestAsoKeywordSeeds;

/// <summary>
/// Handles <see cref="SuggestAsoKeywordSeedsCommand"/>: opens the organization's AI key, reads the listing texts live (a
/// failed read leaves them out), asks <see cref="IListingSuggestionWriter"/> for themed ideas with the reply drafts' model
/// and returns the new, normalized terms. Every answered call is audited with its tokens and cost. No [Log]: the texts are
/// the customer's.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="SettingRepository"></param>
/// <param name="TrackedRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="SuggestionWriter"></param>
/// <param name="AuditTrail"></param>
/// <param name="Localizer"></param>
/// <param name="Options"></param>
public record SuggestAsoKeywordSeedsCommandHandler(IStoreContentService ContentService,
                                                   IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                                   IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                                   ISecretProtector SecretProtector,
                                                   IListingSuggestionWriter SuggestionWriter,
                                                   IAuditTrail AuditTrail,
                                                   IMilvaLocalizer Localizer,
                                                   AiOptions Options) : IInterceptable, ICommandHandler<SuggestAsoKeywordSeedsCommand, AsoKeywordSeedsDto>
{
    /// <summary>
    /// Longest wait for the AI's answer.
    /// </summary>
    public static readonly TimeSpan AiBudget = TimeSpan.FromSeconds(75);

    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IListingSuggestionWriter _suggestionWriter = SuggestionWriter;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly AiOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<AsoKeywordSeedsDto>> Handle(SuggestAsoKeywordSeedsCommand request, CancellationToken cancellationToken)
    {
        var setting = await _settingRepository.OpenAiSettingAsync(_secretProtector, request.OrganizationId, cancellationToken);

        if (setting == null)
            return Response<AsoKeywordSeedsDto>.Error(null, MessageKey.AiKeyNotSet);

        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<AsoKeywordSeedsDto>.Error(null, MessageKey.StoreAppNotFound);

        if (app.Store == StoreKind.Microsoft)
            return Response<AsoKeywordSeedsDto>.Error(null, MessageKey.AsoStoreNotSupported);

        var country = AsoCountries.Normalize(request.Country);
        var language = AsoCountries.Language(country);
        var culture = AsoExtensions.KeywordCulture(country);
        var (locale, texts, errorKey) = await _contentService.ReadAsoListingAsync(app, request.Locale, country, cancellationToken);

        var textMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = texts.Name,
            ["subtitle"] = texts.Subtitle,
            ["keywords"] = texts.Keywords,
            ["description"] = texts.Description,
        }.Where(t => !string.IsNullOrWhiteSpace(t.Value)).ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal);

        var known = AsoExtensions.NormalizeKeywords(request.Seeds, country);

        // The AI call (with the SDK's own retry) must end before the reverse proxy gives up on the request (about 100
        // seconds), so the member gets a clear "AI unavailable" answer instead of a gateway error.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(AiBudget);

        KeywordSeedResult result;

        try
        {
            result = await _suggestionWriter.SuggestKeywordSeedsAsync(new KeywordSeedRequest(setting.ApiKey,
                                                                                            app.AppName,
                                                                                            app.Store,
                                                                                            country,
                                                                                            language,
                                                                                            textMap,
                                                                                            known,
                                                                                            ListingSuggestions.ProfileLines(app.Profile),
                                                                                            string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim(),
                                                                                            _options.ReplyDraftModelFor(setting.ReplyDraftModel)),
                                                                          budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Response<AsoKeywordSeedsDto>.Error(null, ListingSuggestions.FailureKey(ReplyDraftOutcome.Unavailable));
        }

        var seen = known.ToHashSet(StringComparer.Ordinal);
        var seeds = new List<AsoKeywordSeedDto>();

        foreach (var cluster in result.Clusters ?? [])
        {
            foreach (var term in cluster.Terms.Select(t => AsoText.Normalize(t, culture)).Where(t => t != null && t.Length <= AsoTrackedKeyword.KeywordMaxLength && seen.Add(t)))
                seeds.Add(new AsoKeywordSeedDto { Term = term, Source = AsoSeeds.AiSource, Cluster = cluster.Name });
        }

        // Every answered call costs tokens, so it is audited even when its answer is not usable.
        if (result.InputTokens > 0 || result.OutputTokens > 0)
        {
            await _auditTrail.WriteAsync(request.OrganizationId,
                                         AuditAction.AsoKeywordSeedsGenerated,
                                         app.StoreAppId.ToString(),
                                         $"AI keyword ideas for store record {app.StoreAppId} ({country}): {seeds.Count} terms.",
                                         app.Store,
                                         details: new
                                         {
                                             Country = country,
                                             Terms = seeds.Count,
                                             result.Model,
                                             result.InputTokens,
                                             result.OutputTokens,
                                             EstimatedCostUsd = AiModelCatalog.EstimateCostUsd(result.Model, result.InputTokens, result.OutputTokens),
                                             result.Outcome,
                                         },
                                         cancellationToken: cancellationToken);
        }

        if (result.Outcome != ReplyDraftOutcome.Written)
            return Response<AsoKeywordSeedsDto>.Error(null, ListingSuggestions.FailureKey(result.Outcome));

        if (seeds.Count == 0)
            return Response<AsoKeywordSeedsDto>.Error(null, MessageKey.AiSuggestionFailed);

        var tracked = await _trackedRepository.TrackedKeywordSetAsync(app.StoreAppId, country, cancellationToken);

        seeds.ForEach(s => s.Tracked = tracked.Contains(s.Term));

        return Response<AsoKeywordSeedsDto>.Success(new AsoKeywordSeedsDto
        {
            Store = app.Store,
            Country = country,
            Locale = locale,
            Warning = errorKey == null ? null : _localizer[MessageKey.AsoListingUnavailable],
            Seeds = seeds,
            Model = result.Model,
        });
    }
}
