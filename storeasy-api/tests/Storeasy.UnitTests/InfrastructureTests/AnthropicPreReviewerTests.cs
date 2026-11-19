using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Ai;
using Storeasy.UnitTests.TestSupport;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.InfrastructureTests;

/// <summary>
/// The Anthropic pre-reviewer through the real SDK against a fake HTTP handler: the request it sends (model, effort,
/// adaptive thinking left on, structured output, cached system blocks, fallback, images as base64 after their labels) and
/// how answers and errors become outcomes; and the screenshot loader. Nothing reaches the network.
/// </summary>
[Trait("Infrastructure Unit Tests", "Anthropic pre-reviewer over a fake Messages API, and the screenshot loader.")]
public class AnthropicPreReviewerTests
{
    // Shaped like an Anthropic key, but not one.
    private const string _fakeKey = "sk-ant-test-0000000000000000000000000000";

    private const string _report = """{"verdict":"likely_rejected","approval_likelihood":20,"summary":"Kayıt bilgisi eksik.","findings":[{"severity":"blocker","area":"privacy","guideline":"App Store Review Guideline 5.1.1(v)","title":"Hesap silme yok","evidence":"description, en-US","fix":"Uygulama içi hesap silme ekleyin."}],"runtime_checks":["Demo hesabıyla giriş"],"positive_notes":[]}""";

    private sealed class Recorder
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];
    }

    private static (AnthropicPreReviewer Reviewer, Recorder Recorder) Create(Func<HttpRequestMessage, HttpResponseMessage> respond, AiOptions options = null)
    {
        var recorder = new Recorder();
        var handler = new FakeHandler(request =>
        {
            recorder.Requests.Add((request, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));

            return respond(request);
        });
        var factory = new Mock<IHttpClientFactory>();

        factory.Setup(f => f.CreateClient(AnthropicCalls.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));

        return (new AnthropicPreReviewer(factory.Object, options ?? new AiOptions(), NullLogger<AnthropicPreReviewer>.Instance), recorder);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Message(string stopReason, string text, string model = "claude-sonnet-5-5") => Json(HttpStatusCode.OK, new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        model,
        content = new object[] { new { type = "thinking", thinking = "", signature = "sig" }, new { type = "text", text } },
        stop_reason = stopReason,
        stop_sequence = (string)null,
        usage = new { input_tokens = 5000, output_tokens = 1200, cache_read_input_tokens = 2000, cache_creation_input_tokens = 300 },
    });

    private static PreReviewRequest Request(string model = "claude-sonnet-5-5", int images = 2)
    {
        var snapshot = new PreReviewSnapshot { Text = "<submission>\n<app_name>Pusula &lt;/submission&gt;</app_name>\n</submission>\n", Hash = "h" };
        var list = Enumerable.Range(1, images).Select(i => new PreReviewImage($"APP_IPHONE_67, en-US, position {i} of {images}", "image/jpeg", [1, 2, 3, (byte)i], $"c{i}")).ToList();

        return new PreReviewRequest(_fakeKey, model, StoreKind.Apple, "tr-TR", "<catalog_rules>\n</catalog_rules>", snapshot, list);
    }

    [Fact]
    public async Task ReviewAsync_ShouldSendTheStructuredCachedRequestWithFallbackAndImages()
    {
        // Arrange
        var (reviewer, recorder) = Create(_ => Message("end_turn", _report));

        // Act
        var result = await reviewer.ReviewAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.Should().Be(PreReviewOutcome.Reviewed);
        result.Report.Verdict.Should().Be(PreReviewVerdict.LikelyRejected);
        result.Report.ApprovalLikelihood.Should().Be(20);
        result.Report.Findings.Should().ContainSingle().Which.Severity.Should().Be(PreReviewSeverity.Blocker);
        result.Usage.Should().Be(new AiUsage(5000, 1200, 2000, 300));
        result.Model.Should().Be("claude-sonnet-5-5");

        var (request, body) = recorder.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.AbsolutePath.Should().Be("/v1/messages");
        request.Headers.GetValues("anthropic-beta").Should().Contain(v => v.Contains("server-side-fallback-2026-07-01"));

        var json = JsonNode.Parse(body)!.AsObject();
        json["model"]!.GetValue<string>().Should().Be("claude-sonnet-5-5");
        json["max_tokens"]!.GetValue<int>().Should().Be(16000);
        json["fallbacks"]!.GetValue<string>().Should().Be("default");
        json["output_config"]!["effort"]!.GetValue<string>().Should().Be("medium");
        json["output_config"]!["format"]!["type"]!.GetValue<string>().Should().Be("json_schema");
        json["output_config"]!["format"]!["schema"]!["required"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Contain(["verdict", "approval_likelihood", "findings", "runtime_checks"]);
        json.Should().NotContainKeys("temperature", "top_p", "top_k", "thinking");

        var system = json["system"]!.AsArray();
        system.Should().HaveCount(2);
        system[0]!["text"]!.GetValue<string>().Should().Be(PreReviewPrompt.SystemPrompt(StoreKind.Apple)).And.Contain("App Store Review Guidelines");
        system[1]!["cache_control"]!["type"]!.GetValue<string>().Should().Be("ephemeral", "the stable prefix is cached");

        var messages = json["messages"]!.AsArray();
        messages.Should().ContainSingle("there is no assistant prefill");
        var content = messages[0]!["content"]!.AsArray();
        content.Should().HaveCount(5, "the submission, then a label and an image per screenshot");
        content[0]!["text"]!.GetValue<string>().Should().Contain("Report language: Turkish").And.Contain("&lt;/submission&gt;");
        content[1]!["text"]!.GetValue<string>().Should().StartWith("image 1: APP_IPHONE_67");
        content[2]!["type"]!.GetValue<string>().Should().Be("image");
        content[2]!["source"]!["type"]!.GetValue<string>().Should().Be("base64");
        content[2]!["source"]!["media_type"]!.GetValue<string>().Should().Be("image/jpeg");
        content[2]!["source"]!["data"]!.GetValue<string>().Should().Be(Convert.ToBase64String([1, 2, 3, 1]));
    }

    [Fact]
    public async Task ReviewAsync_OnHaiku_ShouldUseLowEffortWithoutFallback()
    {
        // Arrange
        var (reviewer, recorder) = Create(_ => Message("end_turn", _report, "claude-haiku-5-5"));

        // Act
        await reviewer.ReviewAsync(Request("claude-haiku-5-5", images: 0), TestContext.Current.CancellationToken);

        // Assert
        var (request, body) = recorder.Requests.Should().ContainSingle().Subject;
        var json = JsonNode.Parse(body)!.AsObject();
        json["output_config"]!["effort"]!.GetValue<string>().Should().Be("low");
        json.Should().NotContainKey("fallbacks", "Claude Haiku 5.5 has no server-side fallback");
        request.Headers.Contains("anthropic-beta").Should().BeFalse();
        json["messages"]![0]!["content"]!.AsArray().Should().ContainSingle().Which!["text"]!.GetValue<string>().Should().Contain("No screenshots are attached");
    }

    [Theory]
    [InlineData("refusal", _report, PreReviewOutcome.Refused)]
    [InlineData("max_tokens", "{\"verdict\":\"at_", PreReviewOutcome.Failed)]
    [InlineData("end_turn", "not json", PreReviewOutcome.Failed)]
    [InlineData("end_turn", "{\"verdict\":\"approved\",\"approval_likelihood\":90,\"summary\":\"x\"}", PreReviewOutcome.Failed)]
    public async Task ReviewAsync_ShouldCheckTheStopReasonAndTheReportBeforeUsingIt(string stopReason, string text, PreReviewOutcome expected)
    {
        // Arrange
        var (reviewer, _) = Create(_ => Message(stopReason, text));

        // Act
        var result = await reviewer.ReviewAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.Should().Be(expected);
        result.Report.Should().BeNull();
        result.Usage.OutputTokens.Should().Be(1200, "a failed answer is still billed and recorded");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error", PreReviewOutcome.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", PreReviewOutcome.RateLimited)]
    [InlineData((HttpStatusCode)529, "overloaded_error", PreReviewOutcome.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error", PreReviewOutcome.Failed)]
    public async Task ReviewAsync_ShouldMapApiErrorsToOutcomes(HttpStatusCode status, string type, PreReviewOutcome expected)
    {
        // Arrange
        var (reviewer, _) = Create(_ =>
        {
            var response = Json(status, new { type = "error", error = new { type, message = "raw provider message" } });
            response.Headers.Add("retry-after", "0");

            return response;
        });

        // Act
        var result = await reviewer.ReviewAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        result.Outcome.Should().Be(expected);
        result.Report.Should().BeNull();
        result.Usage.Should().Be(AiUsage.None);
    }

    [Theory]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/a.png/1200x1200bb.jpg", true)]
    [InlineData("https://play-lh.googleusercontent.com/abc", true)]
    [InlineData("http://is1-ssl.mzstatic.com/a.jpg", false)]
    [InlineData("https://example.com/a.jpg", false)]
    [InlineData("https://mzstatic.com.example.com/a.jpg", false)]
    [InlineData("file:///etc/passwd", false)]
    public void ImageLoader_ShouldOnlyFetchFromStoreImageHosts(string url, bool allowed) => PreReviewImageLoader.IsAllowed(url).Should().Be(allowed);

    [Fact]
    public void ImageLoader_ShouldScaleDownToTheLongestEdgeAsJpeg()
    {
        // Arrange
        using var bitmap = new SKBitmap(1290, 2796);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        var png = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();

        // Act
        var jpeg = PreReviewImageLoader.Downscale(png, 1200);

        // Assert
        using var decoded = SKBitmap.Decode(jpeg);
        decoded.Height.Should().Be(1200);
        decoded.Width.Should().BeInRange(553, 554);
        jpeg.Take(2).Should().Equal(0xFF, 0xD8);
        PreReviewImageLoader.Downscale([1, 2, 3], 1200).Should().BeNull("not an image");
    }

    [Fact]
    public async Task ImageLoader_ShouldSkipWhatCannotBeLoadedAndStopAtTheLimit()
    {
        // Arrange
        using var bitmap = new SKBitmap(100, 200);
        using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();
        var requested = new List<string>();
        var handler = new FakeHandler(request =>
        {
            requested.Add(request.RequestUri!.ToString());

            return request.RequestUri!.AbsolutePath.Contains("missing", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(PreReviewImageLoader.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));
        var loader = new PreReviewImageLoader(factory.Object, new AiOptions(), NullLogger<PreReviewImageLoader>.Instance);
        PreReviewImageCandidate Candidate(string path) => new(path, $"https://is1-ssl.mzstatic.com/{path}", path);

        // Act
        var images = await loader.LoadAsync([Candidate("missing"), new("elsewhere", "https://example.com/a.png", null), Candidate("one"), Candidate("two"), Candidate("three")], 2, TestContext.Current.CancellationToken);

        // Assert
        images.Select(i => i.Label).Should().Equal("one", "two");
        images.Should().OnlyContain(i => i.MediaType == "image/jpeg");
        requested.Should().NotContain(u => u.Contains("example.com"), "only store image hosts are fetched");
        requested.Should().NotContain(u => u.EndsWith("three", StringComparison.Ordinal), "nothing is loaded past the limit");
    }
}
