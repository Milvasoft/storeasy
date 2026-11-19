using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Ai;

/// <summary>
/// <see cref="IReplyDraftWriter"/> on the Anthropic Messages API with the official C# SDK and the organization's own key.
/// One request per draft: the stable system prompt of <see cref="ReplyDraftPrompt"/>, the review as data, the model and
/// effort of <see cref="AiOptions"/> (Claude Haiku 5.5 at low effort by default), no sampling parameters and no prefill.
/// The stop reason is checked before the text is read, and only text blocks are read. Errors become a
/// <see cref="ReplyDraftOutcome"/>; the provider's answer and the key are never logged or passed on.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public sealed class AnthropicReplyDraftWriter(IHttpClientFactory httpClientFactory, AiOptions options, ILogger<AnthropicReplyDraftWriter> logger) : IReplyDraftWriter
{
    /// <summary>
    /// Name of the HTTP client the SDK sends with.
    /// </summary>
    public const string HttpClientName = AnthropicCalls.HttpClientName;

    // One retry of a 429 or 5xx (the SDK honours Retry-After); someone is waiting for the answer.
    private const int _maxRetries = 1;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly AiOptions _options = options;
    private readonly ILogger<AnthropicReplyDraftWriter> _logger = logger;

    /// <inheritdoc/>
    public async Task<ReplyDraftResult> WriteAsync(ReplyDraftRequest request, CancellationToken cancellationToken = default)
    {
        var client = CreateClient(request.ApiKey);
        var requestedModel = request.Model ?? _options.Model;
        Message response;

        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = requestedModel,
                MaxTokens = _options.MaxTokens,
                System = ReplyDraftPrompt.SystemPrompt,
                OutputConfig = new OutputConfig { Effort = ParseEffort(_options.Effort) },
                Messages = [new UserMessageParam { Content = ReplyDraftPrompt.UserMessage(request) }],
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var outcome = Classify(exception);

            _logger.LogWarning("Anthropic reply draft failed: {Outcome} ({ErrorType}).", outcome, exception.GetType().Name);

            return ReplyDraftResult.Failure(outcome, requestedModel);
        }

        var model = response.Model.Raw();
        var inputTokens = response.Usage?.InputTokens ?? 0;
        var outputTokens = response.Usage?.OutputTokens ?? 0;

        var stopReason = response.StopReason?.Value();

        if (stopReason == StopReason.Refusal)
        {
            _logger.LogInformation("Anthropic declined to write a reply draft.");

            return new ReplyDraftResult(ReplyDraftOutcome.Refused, null, model, inputTokens, outputTokens);
        }

        // An answer cut at max_tokens is half a reply: not offered as a draft.
        if (stopReason != StopReason.EndTurn)
        {
            _logger.LogWarning("Anthropic reply draft stopped early: {StopReason}.", response.StopReason?.Raw());

            return new ReplyDraftResult(ReplyDraftOutcome.Failed, null, model, inputTokens, outputTokens);
        }

        var text = string.Concat(response.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text)).Trim();

        if (text.Length == 0)
            return new ReplyDraftResult(ReplyDraftOutcome.Failed, null, model, inputTokens, outputTokens);

        return new ReplyDraftResult(ReplyDraftOutcome.Written, text, model, inputTokens, outputTokens);
    }

    /// <inheritdoc/>
    public async Task<AiKeyCheck> CheckKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var client = CreateClient(apiKey);

        try
        {
            await client.Models.Retrieve(_options.Model, cancellationToken: cancellationToken);

            return AiKeyCheck.Valid;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var outcome = Classify(exception);

            _logger.LogInformation("Anthropic key check: {Outcome} ({ErrorType}).", outcome, exception.GetType().Name);

            return outcome == ReplyDraftOutcome.KeyRejected ? AiKeyCheck.Rejected : AiKeyCheck.Unverified;
        }
    }

    /// <summary>
    /// Outcome of an exception thrown by the SDK: typed exceptions first, the status code of any other API error next.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static ReplyDraftOutcome Classify(Exception exception) => AnthropicCalls.Classify(exception) switch
    {
        AiCallFailure.KeyRejected => ReplyDraftOutcome.KeyRejected,
        AiCallFailure.RateLimited => ReplyDraftOutcome.RateLimited,
        AiCallFailure.Unavailable => ReplyDraftOutcome.Unavailable,
        _ => ReplyDraftOutcome.Failed,
    };

    private AnthropicClient CreateClient(string apiKey) => AnthropicCalls.CreateClient(_httpClientFactory, apiKey, _options.TimeoutSeconds, _maxRetries);

    private static Effort ParseEffort(string effort) => effort?.Trim().ToLowerInvariant() switch
    {
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Low,
    };
}
