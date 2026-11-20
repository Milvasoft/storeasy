using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Content;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Listings.SuggestListingField;

/// <summary>
/// Handles <see cref="SuggestListingFieldCommand"/>: reads the listing live (as listings/get), opens the organization's
/// AI key, asks <see cref="IListingSuggestionWriter"/> with the reply drafts' model setting and fits the answer into the
/// catalog limit (<see cref="ListingSuggestions.Fit"/>). No [Log]: the listing and the suggestion are the customer's text;
/// the audit entry keeps the field, the model, the token counts and the estimated cost only.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="RulesCatalog"></param>
/// <param name="SettingRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="SuggestionWriter"></param>
/// <param name="AuditTrail"></param>
/// <param name="Options"></param>
public record SuggestListingFieldCommandHandler(IStoreContentService ContentService,
                                                IRulesCatalog RulesCatalog,
                                                IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                                ISecretProtector SecretProtector,
                                                IListingSuggestionWriter SuggestionWriter,
                                                IAuditTrail AuditTrail,
                                                AiOptions Options) : IInterceptable, ICommandHandler<SuggestListingFieldCommand, ListingSuggestionDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IRulesCatalog _rulesCatalog = RulesCatalog;
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IListingSuggestionWriter _suggestionWriter = SuggestionWriter;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly AiOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<ListingSuggestionDto>> Handle(SuggestListingFieldCommand request, CancellationToken cancellationToken)
    {
        // The key first: without it nothing is read from the store.
        var setting = await _settingRepository.OpenAiSettingAsync(_secretProtector, request.OrganizationId, cancellationToken);

        if (setting == null)
            return Response<ListingSuggestionDto>.Error(null, MessageKey.AiKeyNotSet);

        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Texts, cancellationToken, request.VersionId);

        if (!read.IsSuccess)
            return read.Error<ListingSuggestionDto>();

        var app = read.App;
        var snapshot = read.Snapshot;
        var field = ListingSuggestions.Find(app.Store, request.Field);

        if (field == null)
            return Response<ListingSuggestionDto>.Error(null, MessageKey.AiSuggestionNotSupported);

        var locale = snapshot.Locales.FirstOrDefault(l => string.Equals(l, request.Locale?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (locale == null)
            return Response<ListingSuggestionDto>.Error(null, MessageKey.AiSuggestionLocaleNotFound);

        var catalog = _rulesCatalog.Current;
        var (maxLength, maxBytes, minLength) = ListingSuggestions.Limits(field, catalog);
        var before = snapshot.Get(ContentKey.Of(field.Group, locale, field.Field));
        var primary = string.IsNullOrWhiteSpace(snapshot.PrimaryLocale) || string.Equals(snapshot.PrimaryLocale, locale, StringComparison.OrdinalIgnoreCase) ? null : snapshot.PrimaryLocale;

        var result = await _suggestionWriter.SuggestAsync(new ListingSuggestionRequest(setting.ApiKey,
                                                                                       app.Store,
                                                                                       app.AppName,
                                                                                       field.Field,
                                                                                       locale,
                                                                                       snapshot.PrimaryLocale,
                                                                                       maxLength,
                                                                                       maxBytes,
                                                                                       minLength,
                                                                                       ListingSuggestions.RuleLines(field, catalog),
                                                                                       before,
                                                                                       Texts(app.Store, snapshot, locale, except: field.Field),
                                                                                       primary == null ? null : Texts(app.Store, snapshot, primary, except: null),
                                                                                       ListingSuggestions.ProfileLines(app.Profile),
                                                                                       app.Store == StoreKind.Apple ? snapshot.VersionString : null,
                                                                                       string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim(),
                                                                                       _options.ReplyDraftModelFor(setting.ReplyDraftModel)),
                                                       cancellationToken);

        var fitted = result.Outcome == ReplyDraftOutcome.Written ? ListingSuggestions.Fit(field.Field, result.Value, maxLength, maxBytes) : null;

        // Every answered call costs tokens, so it is audited even when its text is not usable.
        if (result.InputTokens > 0 || result.OutputTokens > 0)
        {
            await _auditTrail.WriteAsync(request.OrganizationId,
                                         AuditAction.ListingSuggestionGenerated,
                                         app.StoreAppId.ToString(),
                                         $"AI suggestion for {field.Field} ({locale}) of store record {app.StoreAppId}{(fitted == null ? " was not usable" : string.Empty)}.",
                                         app.Store,
                                         details: new
                                         {
                                             Field = field.Field,
                                             Locale = locale,
                                             result.Model,
                                             result.InputTokens,
                                             result.OutputTokens,
                                             EstimatedCostUsd = AiModelCatalog.EstimateCostUsd(result.Model, result.InputTokens, result.OutputTokens),
                                             Outcome = fitted == null ? result.Outcome == ReplyDraftOutcome.Written ? ReplyDraftOutcome.Failed : result.Outcome : ReplyDraftOutcome.Written,
                                             Shortened = fitted?.Shortened ?? false,
                                         },
                                         cancellationToken: cancellationToken);
        }

        if (result.Outcome != ReplyDraftOutcome.Written)
            return Response<ListingSuggestionDto>.Error(null, ListingSuggestions.FailureKey(result.Outcome));

        if (fitted == null)
            return Response<ListingSuggestionDto>.Error(null, MessageKey.AiSuggestionFailed);

        return Response<ListingSuggestionDto>.Success(new ListingSuggestionDto
        {
            StoreAppId = app.StoreAppId,
            Locale = locale,
            Field = field.Field,
            Before = before,
            Value = fitted.Value.Value,
            Reason = result.Reason,
            Shortened = fitted.Value.Shortened,
            MaxLength = maxLength,
            MaxBytes = maxBytes,
            Model = result.Model,
        });
    }

    private static Dictionary<string, string> Texts(StoreKind store, StoreContentSnapshot snapshot, string locale, string except)
        => ListingFields.Of(store)
                        .Where(f => f.Localized && !f.IsUrl && f.Field != except)
                        .Select(f => (f.Field, Value: snapshot.Get(ContentKey.Of(f.Group, locale, f.Field))))
                        .Where(t => !string.IsNullOrWhiteSpace(t.Value))
                        .ToDictionary(t => t.Field, t => t.Value, StringComparer.Ordinal);
}
