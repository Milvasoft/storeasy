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
        var call = await CompleteAsync(request.ApiKey, request.Model, ListingSuggestionPrompt.SystemPrompt, ListingSuggestionPrompt.UserMessage(request), "listing suggestion", cancellationToken);

        if (call.Outcome != ReplyDraftOutcome.Written)
            return new ListingSuggestionResult(call.Outcome, null, null, call.Model, call.InputTokens, call.OutputTokens);

        if (ListingSuggestionPrompt.Parse(call.Text) is not { } parsed)
        {
            _logger.LogWarning("Anthropic listing suggestion was not the expected JSON object.");

            return new ListingSuggestionResult(ReplyDraftOutcome.Failed, null, null, call.Model, call.InputTokens, call.OutputTokens);
        }

        return new ListingSuggestionResult(ReplyDraftOutcome.Written, parsed.Value, parsed.Reason, call.Model, call.InputTokens, call.OutputTokens);
    }

    /// <inheritdoc/>
    public async Task<CategorySuggestionResult> SuggestCategoriesAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var call = await CompleteAsync(request.ApiKey, request.Model, CategorySuggestionPrompt.SystemPrompt, CategorySuggestionPrompt.UserMessage(request), "category suggestion", cancellationToken);

        if (call.Outcome != ReplyDraftOutcome.Written)
            return new CategorySuggestionResult(call.Outcome, null, null, null, call.Model, call.InputTokens, call.OutputTokens);

        if (CategorySuggestionPrompt.Parse(call.Text) is not { } parsed)
        {
            _logger.LogWarning("Anthropic category suggestion was not the expected JSON object.");

            return new CategorySuggestionResult(ReplyDraftOutcome.Failed, null, null, null, call.Model, call.InputTokens, call.OutputTokens);
        }

        return new CategorySuggestionResult(ReplyDraftOutcome.Written, parsed.Primary, parsed.Secondary, parsed.Reason, call.Model, call.InputTokens, call.OutputTokens);
    }

    private async Task<(ReplyDraftOutcome Outcome, string Text, string Model, long InputTokens, long OutputTokens)> CompleteAsync(string apiKey,
                                                                                                                              string model,
                                                                                                                              string system,
                                                                                                                              string user,
                                                                                                                              string what,
                                                                                                                              CancellationToken cancellationToken)
    {
        var client = AnthropicCalls.CreateClient(_httpClientFactory, apiKey, _options.TimeoutSeconds, _maxRetries);
        var requestedModel = model ?? _options.Model;
        Message response;

        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = requestedModel,
                MaxTokens = Math.Max(_options.MaxTokens, _minMaxTokens),
                System = system,
                OutputConfig = new OutputConfig { Effort = ParseEffort(_options.Effort) },
                Messages = [new UserMessageParam { Content = user }],
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var outcome = AnthropicReplyDraftWriter.Classify(exception);

            _logger.LogWarning("Anthropic {What} failed: {Outcome} ({ErrorType}).", what, outcome, exception.GetType().Name);

            return (outcome, null, requestedModel, 0, 0);
        }

        var answeredModel = response.Model.Raw();
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;
        var stopReason = response.StopReason?.Value();

        if (stopReason == StopReason.Refusal)
        {
            _logger.LogInformation("Anthropic declined to write a {What}.", what);

            return (ReplyDraftOutcome.Refused, null, answeredModel, inputTokens, outputTokens);
        }

        if (stopReason != StopReason.EndTurn)
        {
            _logger.LogWarning("Anthropic {What} stopped early: {StopReason}.", what, response.StopReason?.Raw());

            return (ReplyDraftOutcome.Failed, null, answeredModel, inputTokens, outputTokens);
        }

        var text = string.Concat(response.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));

        return (ReplyDraftOutcome.Written, text, answeredModel, inputTokens, outputTokens);
    }

    private static Effort ParseEffort(string effort) => effort?.Trim().ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Low,
    };
}
