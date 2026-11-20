using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Ai;

/// <summary>
/// <see cref="IListingSuggestionWriter"/> over the Anthropic Messages API, configured as the reply drafts
/// (<see cref="AnthropicReplyDraftWriter"/>): the organization's key, the same HTTP client, timeout, effort and retry.
/// Neither the listing nor the answer is logged.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public sealed class AnthropicListingSuggestionWriter(IHttpClientFactory httpClientFactory, AiOptions options, ILogger<AnthropicListingSuggestionWriter> logger) : IListingSuggestionWriter
{
    // One retry of a 429 or 5xx (the SDK honours Retry-After); someone is waiting for the answer.
    private const int _maxRetries = 1;

    // A 4000 character description plus its reason fits; more would only be cut.
    private const int _minMaxTokens = 3000;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly AiOptions _options = options;
    private readonly ILogger<AnthropicListingSuggestionWriter> _logger = logger;

    /// <inheritdoc/>
    public async Task<ListingSuggestionResult> SuggestAsync(ListingSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var client = AnthropicCalls.CreateClient(_httpClientFactory, request.ApiKey, _options.TimeoutSeconds, _maxRetries);
        var requestedModel = request.Model ?? _options.Model;
        Message response;

        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = requestedModel,
                MaxTokens = Math.Max(_options.MaxTokens, _minMaxTokens),
                System = ListingSuggestionPrompt.SystemPrompt,
                OutputConfig = new OutputConfig { Effort = ParseEffort(_options.Effort) },
                Messages = [new UserMessageParam { Content = ListingSuggestionPrompt.UserMessage(request) }],
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var outcome = AnthropicReplyDraftWriter.Classify(exception);

            _logger.LogWarning("Anthropic listing suggestion failed: {Outcome} ({ErrorType}).", outcome, exception.GetType().Name);

            return ListingSuggestionResult.Failure(outcome, requestedModel);
        }

        var model = response.Model.Raw();
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;
        var stopReason = response.StopReason?.Value();

        if (stopReason == StopReason.Refusal)
        {
            _logger.LogInformation("Anthropic declined to write a listing suggestion.");

            return new ListingSuggestionResult(ReplyDraftOutcome.Refused, null, null, model, inputTokens, outputTokens);
        }

        if (stopReason != StopReason.EndTurn)
        {
            _logger.LogWarning("Anthropic listing suggestion stopped early: {StopReason}.", response.StopReason?.Raw());

            return new ListingSuggestionResult(ReplyDraftOutcome.Failed, null, null, model, inputTokens, outputTokens);
        }

        var text = string.Concat(response.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));

        if (ListingSuggestionPrompt.Parse(text) is not { } parsed)
        {
            _logger.LogWarning("Anthropic listing suggestion was not the expected JSON object.");

            return new ListingSuggestionResult(ReplyDraftOutcome.Failed, null, null, model, inputTokens, outputTokens);
        }

        return new ListingSuggestionResult(ReplyDraftOutcome.Written, parsed.Value, parsed.Reason, model, inputTokens, outputTokens);
    }

    private static Effort ParseEffort(string effort) => effort?.Trim().ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Low,
    };
}
