using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Content;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Listings.SuggestCategories;

/// <summary>
/// Handles <see cref="SuggestCategoriesCommand"/>: reads the categories and the texts live (as categories and listings
/// do), lists the store's categories, opens the organization's AI key and asks <see cref="IListingSuggestionWriter"/> with
/// the reply drafts' model setting. An id the store does not offer is dropped (a secondary) or fails the call (a primary).
/// No [Log]: the texts are the customer's; the audit entry keeps the model, the token counts and the estimated cost only.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="SettingRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="SuggestionWriter"></param>
/// <param name="AuditTrail"></param>
/// <param name="Options"></param>
public record SuggestCategoriesCommandHandler(IStoreContentService ContentService,
                                              IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                              ISecretProtector SecretProtector,
                                              IListingSuggestionWriter SuggestionWriter,
                                              IAuditTrail AuditTrail,
                                              AiOptions Options) : IInterceptable, ICommandHandler<SuggestCategoriesCommand, CategorySuggestionDto>
{
    private static readonly string[] _textFields = ["name", "subtitle", "description", "keywords", "promotionalText"];

    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IListingSuggestionWriter _suggestionWriter = SuggestionWriter;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly AiOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<CategorySuggestionDto>> Handle(SuggestCategoriesCommand request, CancellationToken cancellationToken)
    {
        // The key first: without it nothing is read from the store.
        var setting = await _settingRepository.OpenAiSettingAsync(_secretProtector, request.OrganizationId, cancellationToken);

        if (setting == null)
            return Response<CategorySuggestionDto>.Error(null, MessageKey.AiKeyNotSet);

        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Categories | ContentParts.Texts, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<CategorySuggestionDto>();

        var options = await _contentService.ListOptionsAsync(read.App, StoreOptionKind.Categories, null, cancellationToken);

        if (options.ErrorKey != null)
            return new StoreContentResult(null, null, options.ErrorKey, options.ErrorDetail).Error<CategorySuggestionDto>();

        var categories = options.Options.Where(o => !string.IsNullOrWhiteSpace(o.Key)).Select(o => (Id: o.Key, o.Title)).ToList();

        if (categories.Count == 0)
            return Response<CategorySuggestionDto>.Error(null, MessageKey.AiSuggestionFailed);

        var app = read.App;
        var snapshot = read.Snapshot;
        var currentPrimary = snapshot.Get(ContentKey.Of(ContentGroups.AppInfo, null, "primaryCategory"));
        var currentSecondary = snapshot.Get(ContentKey.Of(ContentGroups.AppInfo, null, "secondaryCategory"));
        var locale = snapshot.PrimaryLocale ?? snapshot.Locales.FirstOrDefault();

        var texts = locale == null
            ? []
            : ListingFields.Of(StoreKind.Apple)
                           .Where(f => _textFields.Contains(f.Field))
                           .Select(f => (f.Field, Value: snapshot.Get(ContentKey.Of(f.Group, locale, f.Field))))
                           .Where(t => !string.IsNullOrWhiteSpace(t.Value))
                           .ToDictionary(t => t.Field, t => t.Value, StringComparer.Ordinal);

        var result = await _suggestionWriter.SuggestCategoriesAsync(new CategorySuggestionRequest(setting.ApiKey,
                                                                                                  app.AppName,
                                                                                                  locale,
                                                                                                  categories,
                                                                                                  currentPrimary,
                                                                                                  currentSecondary,
                                                                                                  texts,
                                                                                                  ListingSuggestions.ProfileLines(app.Profile),
                                                                                                  string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim(),
                                                                                                  _options.ReplyDraftModelFor(setting.ReplyDraftModel)),
                                                                    cancellationToken);

        string Known(string id) => id == null ? null : categories.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)).Id;

        var primary = result.Outcome == ReplyDraftOutcome.Written ? Known(result.Primary) : null;
        var secondary = primary == null ? null : Known(result.Secondary);

        if (string.Equals(secondary, primary, StringComparison.Ordinal))
            secondary = null;

        // Every answered call costs tokens, so it is audited even when its answer is not usable.
        if (result.InputTokens > 0 || result.OutputTokens > 0)
        {
            await _auditTrail.WriteAsync(request.OrganizationId,
                                         AuditAction.CategorySuggestionGenerated,
                                         app.StoreAppId.ToString(),
                                         $"AI category suggestion for store record {app.StoreAppId}{(primary == null ? " was not usable" : string.Empty)}.",
                                         app.Store,
                                         details: new
                                         {
                                             Primary = primary,
                                             Secondary = secondary,
                                             result.Model,
                                             result.InputTokens,
                                             result.OutputTokens,
                                             EstimatedCostUsd = AiModelCatalog.EstimateCostUsd(result.Model, result.InputTokens, result.OutputTokens),
                                             Outcome = primary == null ? result.Outcome == ReplyDraftOutcome.Written ? ReplyDraftOutcome.Failed : result.Outcome : ReplyDraftOutcome.Written,
                                         },
                                         cancellationToken: cancellationToken);
        }

        if (result.Outcome != ReplyDraftOutcome.Written)
            return Response<CategorySuggestionDto>.Error(null, ListingSuggestions.FailureKey(result.Outcome));

        if (primary == null)
            return Response<CategorySuggestionDto>.Error(null, MessageKey.AiSuggestionFailed);

        return Response<CategorySuggestionDto>.Success(new CategorySuggestionDto
        {
            StoreAppId = app.StoreAppId,
            CurrentPrimary = currentPrimary,
            CurrentSecondary = currentSecondary,
            Primary = primary,
            Secondary = secondary,
            Reason = result.Reason,
            Model = result.Model,
        });
    }
}
