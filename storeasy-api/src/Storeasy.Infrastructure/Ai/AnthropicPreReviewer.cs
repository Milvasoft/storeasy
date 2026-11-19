using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Logging;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Models;
using System.Text.Json;

namespace Storeasy.Infrastructure.Ai;

/// <summary>
/// <see cref="IPreReviewer"/> on the Anthropic Messages API (beta path, for the server-side refusal fallback) with the
/// official C# SDK and the organization's own key. One non-streaming request per pre-review:
/// <list type="bullet">
/// <item>system: the store's reviewer prompt and the catalog rules, the second block marked for prompt caching;</item>
/// <item>user: the submission as escaped data, then each screenshot (base64 JPEG) after its text label;</item>
/// <item>adaptive thinking (the default; never disabled), the effort of <see cref="AiOptions.PreReviewEffort"/>, no
/// sampling parameters, no prefill;</item>
/// <item>the report as structured output (<c>output_config.format</c> with <see cref="PreReviewPrompt.OutputSchemaJson"/>);</item>
/// <item><c>fallbacks: "default"</c> with the <c>server-side-fallback-2026-07-01</c> beta on models that support it.</item>
/// </list>
/// The stop reason is checked before the content is read, and only text blocks are read. Errors become a
/// <see cref="PreReviewOutcome"/>; the provider's answer and the key are never logged or passed on.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="logger"></param>
public sealed class AnthropicPreReviewer(IHttpClientFactory httpClientFactory, AiOptions options, ILogger<AnthropicPreReviewer> logger) : IPreReviewer
{
    // One retry of a 429 or 5xx: the job itself is retried on a transient failure, and every attempt costs money.
    private const int _maxRetries = 1;

    private static readonly IReadOnlyDictionary<string, JsonElement> _schema = ParseSchema();

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly AiOptions _options = options;
    private readonly ILogger<AnthropicPreReviewer> _logger = logger;

    /// <inheritdoc/>
    public async Task<PreReviewResult> ReviewAsync(PreReviewRequest request, CancellationToken cancellationToken = default)
    {
        var client = AnthropicCalls.CreateClient(_httpClientFactory, request.ApiKey, _options.PreReviewTimeoutSeconds, _maxRetries);
        BetaMessage response;

        try
        {
            response = await client.Beta.Messages.Create(BuildParams(request), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var failure = AnthropicCalls.Classify(exception);

            _logger.LogWarning("Anthropic pre-review failed: {Outcome} ({ErrorType}).", failure, exception.GetType().Name);

            return PreReviewResult.Failure(Outcome(failure), request.Model);
        }

        var model = response.Model.Raw();
        var usage = new AiUsage(response.Usage?.InputTokens ?? 0,
                                response.Usage?.OutputTokens ?? 0,
                                response.Usage?.CacheReadInputTokens ?? 0,
                                response.Usage?.CacheCreationInputTokens ?? 0);

        var stopReason = response.StopReason?.Raw();

        if (stopReason == "refusal")
        {
            _logger.LogInformation("Anthropic declined the pre-review.");

            return PreReviewResult.Failure(PreReviewOutcome.Refused, model, usage);
        }

        // A report cut at max_tokens is not valid JSON and not a whole review.
        if (stopReason != "end_turn")
        {
            _logger.LogWarning("Anthropic pre-review stopped early: {StopReason}.", stopReason);

            return PreReviewResult.Failure(PreReviewOutcome.Failed, model, usage);
        }

        var text = string.Concat(response.Content.Select(block => block.TryPickText(out var textBlock) ? textBlock.Text : null).Where(t => t != null));
        var report = PreReviewReport.Parse(text);

        if (report == null)
        {
            _logger.LogWarning("Anthropic pre-review answer is not a valid report.");

            return PreReviewResult.Failure(PreReviewOutcome.Failed, model, usage);
        }

        return new PreReviewResult(PreReviewOutcome.Reviewed, report, model, usage);
    }

    /// <summary>
    /// The request of one pre-review.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public MessageCreateParams BuildParams(PreReviewRequest request)
    {
        var content = new List<BetaContentBlockParam>
        {
            new BetaTextBlockParam { Text = PreReviewPrompt.UserMessage(request.Snapshot, request.Language, [.. request.Images.Select(i => i.Label)]) },
        };

        for (var index = 0; index < request.Images.Count; index++)
        {
            var image = request.Images[index];

            content.Add(new BetaTextBlockParam { Text = PreReviewPrompt.ImageLabel(index, image.Label) });
            content.Add(new BetaImageBlockParam { Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(image.Data), MediaType = image.MediaType } });
        }

        var fallback = _options.PreReviewFallback && AiModelCatalog.Find(request.Model)?.SupportsFallback == true;

        var parameters = new MessageCreateParams
        {
            Model = request.Model,
            MaxTokens = _options.PreReviewMaxTokens,
            System = new List<BetaTextBlockParam>
            {
                new() { Text = PreReviewPrompt.SystemPrompt(request.Store) },
                new() { Text = request.RulesBlock, CacheControl = new BetaCacheControlEphemeral() },
            },
            OutputConfig = new BetaOutputConfig
            {
                Effort = ParseEffort(_options.PreReviewEffortOf(request.Model)),
                Format = new BetaJsonOutputFormat { Schema = _schema },
            },
            Messages = [new BetaUserMessageParam { Content = content }],
        };

        // Only on models that support it: a null value would still be sent.
        return fallback ? parameters with { Betas = [AnthropicBeta.ServerSideFallback2026_07_01], Fallbacks = new BetaFallbacksParam(new Default()) } : parameters;
    }

    private static PreReviewOutcome Outcome(AiCallFailure failure) => failure switch
    {
        AiCallFailure.KeyRejected => PreReviewOutcome.KeyRejected,
        AiCallFailure.RateLimited => PreReviewOutcome.RateLimited,
        AiCallFailure.Unavailable => PreReviewOutcome.Unavailable,
        _ => PreReviewOutcome.Failed,
    };

    private static Effort ParseEffort(string effort) => effort?.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    private static IReadOnlyDictionary<string, JsonElement> ParseSchema()
    {
        using var document = JsonDocument.Parse(PreReviewPrompt.OutputSchemaJson);

        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }
}
