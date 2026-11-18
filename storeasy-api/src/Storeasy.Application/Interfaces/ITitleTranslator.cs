namespace Storeasy.Application.Interfaces;

/// <summary>
/// Suggests captions of a mockup template in another locale (studio caption suggestions, docs 12.8). Behind a kill switch:
/// the endpoint answers 503 when the <c>Studio.AiCaptions</c> feature flag is off or no translator is available. The
/// default implementation is a no-op without any paid service; a provider is plugged in by registering another
/// implementation. Suggestions are never saved by themselves: the client shows them as unsaved cells.
/// </summary>
public interface ITitleTranslator
{
    /// <summary>
    /// Whether a provider is configured and can be called.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Suggests texts for <paramref name="request"/>. Only caption texts are sent, never screenshots.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<TitleSuggestion>> SuggestAsync(TitleSuggestionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Captions to suggest a translation for.
/// </summary>
/// <param name="TargetLocale">Locale to write in.</param>
/// <param name="Items">The fields to suggest.</param>
public sealed record TitleSuggestionRequest(string TargetLocale, IReadOnlyList<TitleSuggestionItem> Items);

/// <summary>
/// One caption field to suggest.
/// </summary>
/// <param name="SlotOrder">Slot position.</param>
/// <param name="Field"><c>title</c> or <c>subtitle</c>.</param>
/// <param name="Sources">Text of the field by source locale.</param>
/// <param name="Current">Current text in the target locale, if any.</param>
/// <param name="MaxLength">Soft character limit of the field.</param>
public sealed record TitleSuggestionItem(int SlotOrder, string Field, IReadOnlyDictionary<string, string> Sources, string Current, int MaxLength);

/// <summary>
/// A suggested text.
/// </summary>
/// <param name="SlotOrder">Slot position.</param>
/// <param name="Field"><c>title</c> or <c>subtitle</c>.</param>
/// <param name="Text">Suggested text.</param>
/// <param name="Reason">Short reason (e.g. shortened to fit two lines).</param>
public sealed record TitleSuggestion(int SlotOrder, string Field, string Text, string Reason);
