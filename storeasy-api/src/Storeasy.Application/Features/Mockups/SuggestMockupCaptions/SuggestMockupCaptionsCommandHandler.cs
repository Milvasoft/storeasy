using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.SuggestMockupCaptions;

/// <summary>
/// Handles <see cref="SuggestMockupCaptionsCommand"/>. Only caption texts go to the translator, never screenshots. The
/// translator answers 503 (<see cref="MessageKey.TitleTranslatorUnavailable"/>) when none is configured; the controller's
/// feature flag answers 503 before that when the kill switch is off.
/// </summary>
/// <param name="TemplateRepository"></param>
/// <param name="TitleTranslator"></param>
[Log]
public record SuggestMockupCaptionsCommandHandler(IStoreasyRepositoryBase<MockupTemplate> TemplateRepository, ITitleTranslator TitleTranslator) : IInterceptable, ICommandHandler<SuggestMockupCaptionsCommand, MockupCaptionSuggestionDto>
{
    private const string _title = "title";
    private const string _subtitle = "subtitle";

    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = TemplateRepository;
    private readonly ITitleTranslator _titleTranslator = TitleTranslator;

    /// <inheritdoc/>
    public async Task<Response<MockupCaptionSuggestionDto>> Handle(SuggestMockupCaptionsCommand request, CancellationToken cancellationToken)
    {
        if (!_titleTranslator.IsAvailable)
            return Response<MockupCaptionSuggestionDto>.Error(null, MessageKey.TitleTranslatorUnavailable);

        var template = await _templateRepository.GetMockupSnapshotAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null)
            return Response<MockupCaptionSuggestionDto>.Error(null, MessageKey.MockupTemplateNotFound);

        var target = template.Locales.FirstOrDefault(l => string.Equals(l, request.TargetLocale, StringComparison.OrdinalIgnoreCase));
        var sources = request.SourceLocales ?? [template.PrimaryLocale, .. template.Locales.Where(l => l.StartsWith("en", StringComparison.OrdinalIgnoreCase))];

        if (target == null || sources.Any(s => !template.Locales.Contains(s, StringComparer.OrdinalIgnoreCase)))
            return Response<MockupCaptionSuggestionDto>.Error(null, MessageKey.MockupLocaleInvalid);

        sources = [.. sources.Where(s => !string.Equals(s, target, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)];

        string Text(string locale, int slot, string field)
        {
            var caption = template.Captions.FirstOrDefault(c => c.SlotOrder == slot && string.Equals(c.Locale, locale, StringComparison.OrdinalIgnoreCase));

            return field == _title ? caption?.Title : caption?.Subtitle;
        }

        var slots = request.SlotOrders ?? [.. Enumerable.Range(1, template.SlotCount).Where(slot => string.IsNullOrWhiteSpace(Text(target, slot, _title)))];
        var items = slots.Where(slot => slot <= template.SlotCount)
                         .SelectMany(slot => new[] { _title, _subtitle }.Select(field => new TitleSuggestionItem(slot,
                                                                                                                field,
                                                                                                                sources.Select(s => (Locale: s, Text: Text(s, slot, field)))
                                                                                                                       .Where(s => !string.IsNullOrWhiteSpace(s.Text))
                                                                                                                       .ToDictionary(s => s.Locale, s => s.Text),
                                                                                                                Text(target, slot, field),
                                                                                                                field == _title ? 30 : 40)))
                         .Where(item => item.Sources.Count > 0)
                         .ToList();

        var suggestions = items.Count == 0 ? [] : await _titleTranslator.SuggestAsync(new TitleSuggestionRequest(target, items), cancellationToken);

        return Response<MockupCaptionSuggestionDto>.Success(new MockupCaptionSuggestionDto
        {
            TargetLocale = target,
            Items = [.. suggestions.Select(s => new MockupCaptionSuggestionItemDto
            {
                SlotOrder = s.SlotOrder,
                Field = s.Field,
                Before = Text(target, s.SlotOrder, s.Field),
                After = s.Text,
                Reason = s.Reason,
            })],
        });
    }
}
