using FluentAssertions;
using Moq;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Rules;
using Storeasy.Infrastructure.StoreClients;
using Storeasy.Infrastructure.Stores;
using Storeasy.UnitTests.TestSupport;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Events phase store calls against fake HTTP: webhooks, versions, reviews, TestFlight.")]
public class StoreEventsGatewayTests
{
    private static string AppleSecret()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId = "ABC123DEFG", privateKey = key.ExportPkcs8PrivateKeyPem() });
    }

    private static string GoogleSecret()
    {
        using var key = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string> { ["type"] = "service_account", ["project_id"] = "demo", ["private_key"] = key.ExportPkcs8PrivateKeyPem(), ["client_email"] = "s@demo.iam.gserviceaccount.com", ["token_uri"] = "https://oauth2.googleapis.com/token" });
    }

    private static (StoreEventsGateway Gateway, List<(HttpMethod Method, string Url, string Body)> Requests) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var requests = new List<(HttpMethod, string, string)>();
        var handler = new FakeHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri.ToString(), request.Content?.ReadAsStringAsync().Result));
            return respond(request);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(StoreGateway.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));

        return (new StoreEventsGateway(new StoreApiClient(factory.Object, TimeProvider.System)), requests);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task UpsertWebhook_ShouldCreateWhenTheOldOneIsGone()
    {
        // Arrange
        var (gateway, requests) = Create(request => request.Method == HttpMethod.Patch ? Json("""{"errors":[{"detail":"gone"}]}""", HttpStatusCode.NotFound) : Json("""{"data":{"id":"wh-2"}}"""));

        // Act
        var id = await gateway.UpsertWebhookAsync(AppleSecret(), "123", "wh-1", new StoreWebhookRegistration("storeasy", "https://s.example.com/api/webhooks/apple/whe_x", "whsec_y", ["APP_STORE_VERSION_APP_VERSION_STATE_UPDATED"]), TestContext.Current.CancellationToken);

        // Assert
        id.Should().Be("wh-2");
        requests.Select(r => r.Method).Should().Equal(HttpMethod.Patch, HttpMethod.Post);
        requests[1].Body.Should().Contain("\"secret\":\"whsec_y\"").And.Contain("\"type\":\"apps\",\"id\":\"123\"");
    }

    [Fact]
    public async Task GetVersion_ShouldReadTheAppAndBuild()
    {
        // Arrange
        var (gateway, requests) = Create(_ => Json("""
            {"data":{"type":"appStoreVersions","id":"v1","attributes":{"versionString":"1.4","platform":"IOS","appVersionState":"REJECTED","createdDate":"2026-11-10T10:00:00Z"},
              "relationships":{"app":{"data":{"type":"apps","id":"6740"}},"build":{"data":{"type":"builds","id":"b1"}}}},
             "included":[{"type":"builds","id":"b1","attributes":{"version":"52"}}]}
            """));

        // Act
        var version = await gateway.GetVersionAsync(AppleSecret(), "v1", TestContext.Current.CancellationToken);

        // Assert
        version.Should().Be(new StoreVersionRecord("v1", "6740", "1.4", "IOS", "REJECTED", "52", new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc)));
        requests.Single().Url.Should().Contain("/v1/appStoreVersions/v1?include=app,build");
    }

    [Fact]
    public async Task AppleReviews_ShouldIncludeTheResponse()
    {
        // Arrange
        var (gateway, _) = Create(_ => Json("""
            {"data":[{"type":"customerReviews","id":"r1","attributes":{"rating":2,"title":"Bug","body":"Crashes","reviewerNickname":"mert","createdDate":"2026-11-17T21:07:00Z","territory":"TUR"},
              "relationships":{"response":{"data":{"type":"customerReviewResponses","id":"resp1"}}}}],
             "included":[{"type":"customerReviewResponses","id":"resp1","attributes":{"responseBody":"Thanks","lastModifiedDate":"2026-11-18T09:00:00Z"}}],"links":{}}
            """));

        // Act
        var review = (await gateway.ListReviewsAsync(StoreKind.Apple, AppleSecret(), "6740", TestContext.Current.CancellationToken)).Single();

        // Assert
        review.Should().Match<StoreReviewRecord>(r => r.ExternalId == "r1" && r.Rating == 2 && r.AuthorName == "mert" && r.Territory == "TUR" && r.ReplyExternalId == "resp1" && r.ReplyBody == "Thanks");
    }

    [Fact]
    public async Task GoogleReviews_ShouldPageAndReplyThroughTheReplyEndpoint()
    {
        // Arrange
        var (gateway, requests) = Create(request => request.RequestUri.Host == "oauth2.googleapis.com"
            ? Json("""{"access_token":"ya29.x"}""")
            : request.RequestUri.AbsolutePath.EndsWith(":reply")
                ? Json("""{"result":{"replyText":"Thanks","lastEdited":{"seconds":"1763456400"}}}""")
                : request.RequestUri.Query.Contains("token=")
                    ? Json("""{"reviews":[{"reviewId":"g2","authorName":"B","comments":[{"userComment":{"text":"ok","starRating":4,"lastModified":{"seconds":"1763400000"}}}]}]}""")
                    : Json("""{"reviews":[{"reviewId":"g1","authorName":"Ayşe K.","comments":[{"userComment":{"text":"\tHatırlatıcı yok","starRating":2,"reviewerLanguage":"tr","appVersionName":"3.1.2","lastModified":{"seconds":"1763400000"},"deviceMetadata":{"productName":"Pixel 8"}}},{"developerComment":{"text":"Sorry","lastModified":{"seconds":"1763410000"}}}]}],"tokenPagination":{"nextPageToken":"p2"}}"""));

        // Act
        var reviews = await gateway.ListReviewsAsync(StoreKind.Google, GoogleSecret(), "com.example.mola", TestContext.Current.CancellationToken);
        var reply = await gateway.ReplyAsync(StoreKind.Google, GoogleSecret(), "com.example.mola", "g1", null, "Thanks", TestContext.Current.CancellationToken);

        // Assert
        reviews.Should().HaveCount(2);
        reviews[0].Should().Match<StoreReviewRecord>(r => r.Body == "Hatırlatıcı yok" && r.Locale == "tr" && r.Device == "Pixel 8" && r.AppVersion == "3.1.2" && r.ReplyBody == "Sorry");
        reply.Date.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1763456400).UtcDateTime);
        requests.Should().Contain(r => r.Url.EndsWith("/reviews/g1:reply") && r.Body.Contains("\"replyText\":\"Thanks\""));
    }

    [Fact]
    public async Task Reviews_ShouldRefuseMicrosoftAndMapErrors()
    {
        // Arrange
        var (gateway, _) = Create(_ => Json("""{"errors":[{"detail":"rate"}]}""", HttpStatusCode.TooManyRequests));

        // Act
        var microsoft = () => gateway.ListReviewsAsync(StoreKind.Microsoft, "{}", "x", TestContext.Current.CancellationToken);
        var limited = () => gateway.ListReviewsAsync(StoreKind.Apple, AppleSecret(), "x", TestContext.Current.CancellationToken);

        // Assert
        (await microsoft.Should().ThrowAsync<StoreCallException>()).Which.Transient.Should().BeFalse();
        (await limited.Should().ThrowAsync<StoreCallException>()).Which.Should().Match<StoreCallException>(e => e.Transient && e.StatusCode == 429);
    }

    [Fact]
    public void BetaBuild_ShouldMapReviewStateAndWhatToTest()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {"data":[{"type":"builds","id":"b1","attributes":{"version":"37","expirationDate":"2026-11-24T10:00:00Z","expired":false},
              "relationships":{"betaAppReviewSubmission":{"data":{"type":"betaAppReviewSubmissions","id":"s1"}},"betaBuildLocalizations":{"data":[{"type":"betaBuildLocalizations","id":"l1"}]}}}],
             "included":[{"type":"betaAppReviewSubmissions","id":"s1","attributes":{"betaReviewState":"APPROVED"}},{"type":"betaBuildLocalizations","id":"l1","attributes":{"locale":"tr","whatsNew":"Davet"}}]}
            """);

        // Act
        var build = StoreEventsGateway.ParseBetaBuild(document.RootElement.GetProperty("data")[0], [.. document.RootElement.GetProperty("included").EnumerateArray()]);

        // Assert
        build.ReviewState.Should().Be(BetaReviewState.Approved);
        build.WhatToTest.Should().ContainSingle(w => w.Locale == "tr" && w.Text == "Davet");
        build.ExpirationDate.Should().Be(new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task WhatToTest_ShouldPatchAnExistingLocalizationAndPostANewOne()
    {
        // Arrange
        var (gateway, requests) = Create(request => request.Method == HttpMethod.Get
            ? Json("""{"data":[{"type":"betaBuildLocalizations","id":"l1","attributes":{"locale":"en-US"}}],"links":{}}""")
            : Json("""{"data":{"id":"l2"}}"""));

        // Act
        await gateway.ApplyAsync(AppleSecret(), "6740", new TestFlightChange { Operation = TestFlightOperation.WhatToTest, BuildExternalId = "b1", Locale = "en-US", Text = "Try it" }, TestContext.Current.CancellationToken);
        var created = await gateway.ApplyAsync(AppleSecret(), "6740", new TestFlightChange { Operation = TestFlightOperation.WhatToTest, BuildExternalId = "b1", Locale = "tr", Text = "Dene" }, TestContext.Current.CancellationToken);

        // Assert
        requests.Should().Contain(r => r.Method == HttpMethod.Patch && r.Url.EndsWith("/v1/betaBuildLocalizations/l1") && r.Body.Contains("Try it"));
        requests.Should().Contain(r => r.Method == HttpMethod.Post && r.Url.EndsWith("/v1/betaBuildLocalizations") && r.Body.Contains("\"locale\":\"tr\""));
        created.Should().Be("l2");
    }

    [Fact]
    public async Task UpdateTesters_ShouldAddAnExistingTesterToTheGroupOnConflict()
    {
        // Arrange
        var (gateway, requests) = Create(request => request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/v1/betaTesters"
            ? Json("""{"errors":[{"detail":"exists"}]}""", HttpStatusCode.Conflict)
            : request.Method == HttpMethod.Get ? Json("""{"data":[{"type":"betaTesters","id":"t9"}],"links":{}}""") : Json("{}"));

        // Act
        await gateway.ApplyAsync(AppleSecret(), "6740", new TestFlightChange { Operation = TestFlightOperation.UpdateTesters, GroupExternalId = "g1", AddTesters = [new BetaTesterInvite("a@example.com", "A", null)], RemoveTesterExternalIds = ["t1"] }, TestContext.Current.CancellationToken);

        // Assert
        requests.Should().Contain(r => r.Method == HttpMethod.Post && r.Url.EndsWith("/v1/betaGroups/g1/relationships/betaTesters") && r.Body.Contains("t9"));
        requests.Should().Contain(r => r.Method == HttpMethod.Delete && r.Url.EndsWith("/v1/betaGroups/g1/relationships/betaTesters") && r.Body.Contains("t1"));
    }

    [Fact]
    public void ContentNormalizer_ShouldDropMarkupAndScripts()
    {
        // Act
        var text = ContentNormalizer.Normalize("<html><head><style>p{}</style><script>alert(1)</script></head><body><h1>Upcoming</h1><p>Xcode&nbsp;26 &amp; iOS 26</p><!-- x --></body></html>", 1000);

        // Assert
        text.Should().Be("Upcoming\nXcode 26 & iOS 26");
        text.Should().NotContain("alert").And.NotContain("<");
    }

    [Theory]
    [InlineData("https://developer.apple.com/news/", true)]
    [InlineData("https://support.google.com/googleplay/android-developer/answer/1", true)]
    [InlineData("http://developer.apple.com/news/", false)]
    [InlineData("https://developer.apple.com.evil.example/x", false)]
    [InlineData("https://example.com/", false)]
    public void RuleSourceFetcher_ShouldOnlyAllowOfficialHttpsHosts(string url, bool allowed)
        => new RuleSourceFetcher(new Mock<IHttpClientFactory>().Object, new Storeasy.Application.Utils.Models.RuleWatcherOptions()).IsAllowed(url).Should().Be(allowed);

    [Fact]
    public async Task RuleSourceFetcher_ShouldNotFollowRedirectsOffTheAllowList()
    {
        // Arrange
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://evil.example.com/") } });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(RuleSourceFetcher.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));
        var fetcher = new RuleSourceFetcher(factory.Object, new Storeasy.Application.Utils.Models.RuleWatcherOptions());

        // Act
        var act = () => fetcher.FetchAsync("https://developer.apple.com/news/", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
