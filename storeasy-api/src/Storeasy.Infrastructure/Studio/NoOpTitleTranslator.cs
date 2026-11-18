using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// The default <see cref="ITitleTranslator"/>: no provider, no paid service, nothing leaves the host. The suggestion
/// endpoint answers 503 while this is registered. A real provider replaces the registration.
/// </summary>
public sealed class NoOpTitleTranslator : ITitleTranslator
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public Task<IReadOnlyList<TitleSuggestion>> SuggestAsync(TitleSuggestionRequest request, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TitleSuggestion>>([]);
}
