using FluentAssertions;
using Milvasoft.Core.Abstractions;
using Moq;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using Storeasy.Infrastructure.Stores.Microsoft;
using Storeasy.UnitTests.TestSupport;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Microsoft Store client against a fake Microsoft (no network).")]
public class MicrosoftStoreClientTests
{
    private const string _tenantId = "8d6b2e5a-1111-4222-8333-9c4e2f2a3c07";
    private const string _clientId = "0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12";
    private const string _clientSecret = "super-secret-value~123";
    private const string _sellerId = "40291187";

    private static readonly DateTimeOffset _now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    public static string Secret(string clientSecret = _clientSecret) => JsonSerializer.Serialize(new { tenantId = _tenantId, clientId = _clientId, clientSecret, sellerId = _sellerId });

    private sealed class Harness
    {
        public MicrosoftStoreClient Client { get; init; }

        public MicrosoftTokenCache Cache { get; init; }

        public List<HttpRequestMessage> RequestsSource { get; init; }

        public List<string> BodiesSource { get; init; }

        public List<string> LogsSource { get; init; }
    }

    private static Harness Create(Func<HttpRequestMessage, HttpResponseMessage> respond, MicrosoftTokenCache cache = null)
    {
        var requests = new List<HttpRequestMessage>();
        var bodies = new List<string>();
        var handler = new FakeHandler(request =>
        {
            requests.Add(request);
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return respond(request);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(MicrosoftStoreClient.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));

        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(_now);

        var logs = new List<string>();
        var logger = new Mock<IMilvaLogger>();
        logger.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<object[]>())).Callback<string, object[]>((template, args) => logs.Add($"{template} {string.Join(" ", args ?? [])}"));

        cache ??= new MicrosoftTokenCache();

        var options = new MicrosoftStoreOptions { RetryBaseDelayMilliseconds = 0, MaxRetryDelaySeconds = 0, StatusPollIntervalMilliseconds = 0 };
        var client = new MicrosoftStoreClient(factory.Object, cache, options, TimeProvider.System, logger.Object);

        return new Harness { Client = client, Cache = cache, RequestsSource = requests, BodiesSource = bodies, LogsSource = logs };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status) { Content = new StringContent(body is string text ? text : JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Token(string token = "access-token-1") => Json(HttpStatusCode.OK, new { access_token = token, expires_in = 3599, token_type = "Bearer" });

    private static bool IsToken(HttpRequestMessage request) => request.RequestUri.Host == "login.microsoftonline.com";

    [Fact]
    public async Task Calls_ShouldUseTheV2TokenEndpointWithTheScopeOfEachApi_AndCacheTheTokens()
    {
        var harness = Create(request => IsToken(request)
            ? Token()
            : request.RequestUri.Host == "api.store.microsoft.com"
                ? Json(HttpStatusCode.OK, new { isSuccess = true, errors = Array.Empty<object>(), responseData = new { isReady = true, ongoingSubmissionId = "" } })
                : Json(HttpStatusCode.OK, new { id = "9NBLGGH4R315", primaryName = "Pusula" }));

        await harness.Client.GetApplicationAsync(Secret(), "9NBLGGH4R315", TestContext.Current.CancellationToken);
        await harness.Client.GetApplicationAsync(Secret(), "9NBLGGH4R315", TestContext.Current.CancellationToken);
        var status = await harness.Client.GetProductStatusAsync(Secret(), "9P4K7Q2MZL1D", TestContext.Current.CancellationToken);

        var tokenRequests = harness.RequestsSource.Where(IsToken).ToList();
        var tokenBodies = harness.RequestsSource.Select((r, i) => (r, harness.BodiesSource[i])).Where(x => IsToken(x.r)).Select(x => x.Item2).ToList();

        tokenRequests.Should().HaveCount(2, "one token per scope, reused by the second call");
        tokenRequests.Should().OnlyContain(r => r.RequestUri.AbsolutePath == $"/{_tenantId}/oauth2/v2.0/token");
        tokenBodies.Should().Contain(b => b.Contains("scope=https%3A%2F%2Fmanage.devcenter.microsoft.com%2F.default") && b.Contains("grant_type=client_credentials"));
        tokenBodies.Should().Contain(b => b.Contains("scope=https%3A%2F%2Fapi.store.microsoft.com%2F.default"));
        harness.RequestsSource.Single(r => r.RequestUri.Host == "api.store.microsoft.com").Headers.GetValues("X-Seller-Account-Id").Should().Equal(_sellerId);
        status.IsReady.Should().BeTrue();
        harness.Cache.Count.Should().Be(2);
    }

    [Fact]
    public async Task TooManyRequests_ShouldBeRetried_UntilTheStoreAnswers()
    {
        var calls = 0;
        var harness = Create(request =>
        {
            if (IsToken(request))
                return Token();

            if (++calls < 3)
            {
                var busy = Json(HttpStatusCode.TooManyRequests, new { code = "TooManyRequests", message = "slow down" });
                busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return busy;
            }

            return Json(HttpStatusCode.OK, new { id = "9NBLGGH4R315" });
        });

        var app = await harness.Client.GetApplicationAsync(Secret(), "9NBLGGH4R315", TestContext.Current.CancellationToken);

        app.Id.Should().Be("9NBLGGH4R315");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ServerErrors_ShouldFailTransiently_AfterTheLastAttempt()
    {
        var harness = Create(request => IsToken(request) ? Token() : Json(HttpStatusCode.ServiceUnavailable, new { message = "down" }));

        var act = () => harness.Client.GetApplicationAsync(Secret(), "9NBLGGH4R315", TestContext.Current.CancellationToken);

        var error = (await act.Should().ThrowAsync<StoreCallException>()).Which;

        error.Transient.Should().BeTrue();
        error.StatusCode.Should().Be(503);
        harness.RequestsSource.Count(r => !IsToken(r)).Should().Be(4);
    }

    [Fact]
    public async Task Conflict_ShouldBePermanent_WithTheStoreMessage()
    {
        var harness = Create(request => IsToken(request) ? Token() : Json(HttpStatusCode.Conflict, new { code = "InvalidState", message = "The app uses mandatory updates." }));

        var act = () => harness.Client.CommitSubmissionAsync(Secret(), new MsSubmissionRef { Target = MsSubmissionTarget.App, ApplicationId = "9NBLGGH4R315", SubmissionId = "1" }, TestContext.Current.CancellationToken);

        var error = (await act.Should().ThrowAsync<StoreCallException>()).Which;

        error.Transient.Should().BeFalse();
        error.StatusCode.Should().Be(409);
        error.Message.Should().Contain("mandatory updates");
        harness.RequestsSource.Count(r => !IsToken(r)).Should().Be(1);
    }

    [Fact]
    public async Task Unauthorized_ShouldGetAFreshTokenOnce()
    {
        var tokens = 0;
        var harness = Create(request =>
        {
            if (IsToken(request))
                return Token($"token-{++tokens}");

            return request.Headers.Authorization.Parameter == "token-1" ? Json(HttpStatusCode.Unauthorized, new { message = "expired" }) : Json(HttpStatusCode.OK, new { id = "9NBLGGH4R315" });
        });

        var app = await harness.Client.GetApplicationAsync(Secret(), "9NBLGGH4R315", TestContext.Current.CancellationToken);

        app.Id.Should().Be("9NBLGGH4R315");
        tokens.Should().Be(2);
    }

    [Fact]
    public async Task ListApplications_ShouldFollowNextLinks()
    {
        var harness = Create(request => IsToken(request)
            ? Token()
            : request.RequestUri.Query.Contains("skip=1")
                ? Json(HttpStatusCode.OK, new { value = new[] { new { id = "B" } } })
                : Json(HttpStatusCode.OK, new Dictionary<string, object> { ["value"] = new[] { new { id = "A", pendingApplicationSubmission = new { id = "77" } } }, ["@nextLink"] = "applications?skip=1&top=1" }));

        var apps = await harness.Client.ListApplicationsAsync(Secret(), TestContext.Current.CancellationToken);

        apps.Select(a => a.Id).Should().Equal("A", "B");
        apps[0].PendingSubmissionId.Should().Be("77");
    }

    [Fact]
    public async Task StoreApiFailureEnvelope_ShouldThrowPermanently()
    {
        var harness = Create(request => IsToken(request) ? Token() : Json(HttpStatusCode.OK, new { isSuccess = false, errors = new[] { new { code = "InvalidPackage", message = "Bad URL", target = "packageUrl" } } }));

        var act = () => harness.Client.CommitInstallerPackagesAsync(Secret(), "9P4K7Q2MZL1D", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StoreCallException>()).Which.Message.Should().Contain("InvalidPackage Bad URL");
    }

    [Fact]
    public async Task Verify_ShouldBeRejected_WhenPartnerCenterRefusesTheApplication()
    {
        var harness = Create(request => IsToken(request) ? Token() : Json(HttpStatusCode.Forbidden, new { message = "The application has no role." }));

        var verification = await harness.Client.VerifyAsync(Secret(), TestContext.Current.CancellationToken);

        verification.Status.Should().Be(ConnectionStatus.Rejected);
        verification.Error.Should().Contain("no role");
    }

    [Fact]
    public async Task Verify_ShouldBeRejected_WhenEntraRefusesTheSecret()
    {
        var harness = Create(request => Json(HttpStatusCode.Unauthorized, new { error = "invalid_client", error_description = "AADSTS7000215: Invalid client secret provided." }));

        var verification = await harness.Client.VerifyAsync(Secret(), TestContext.Current.CancellationToken);

        verification.Status.Should().Be(ConnectionStatus.Rejected);
        verification.Error.Should().Contain("AADSTS7000215").And.NotContain(_clientSecret);
    }

    [Fact]
    public async Task Logs_ShouldNotCarrySecretsTokensOrSasSignatures()
    {
        var harness = Create(request => IsToken(request) ? Token("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.c2lnbmF0dXJl") : Json(HttpStatusCode.InternalServerError, new { message = "boom" }));

        var act = () => harness.Client.UploadSubmissionFilesAsync("https://blob.example.com/ingestion/x?sv=2014&sig=SECRETSIG", new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<StoreCallException>();

        harness.LogsSource.Should().NotBeEmpty();
        string.Join("\n", harness.LogsSource).Should().NotContain("SECRETSIG").And.NotContain(_clientSecret).And.NotContain("eyJhbGci");
    }

    [Fact]
    public async Task Upload_ShouldPutOneBlockBlob()
    {
        var harness = Create(_ => new HttpResponseMessage(HttpStatusCode.Created));

        await harness.Client.UploadSubmissionFilesAsync("https://blob.example.com/ingestion/x?sig=abc", new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken);

        var upload = harness.RequestsSource.Single();

        upload.Method.Should().Be(HttpMethod.Put);
        upload.Headers.GetValues("x-ms-blob-type").Should().Equal("BlockBlob");
        upload.Headers.Authorization.Should().BeNull("the SAS address carries the authorization");
    }

    [Fact]
    public async Task Download_ShouldRefusePrivateHosts()
    {
        var harness = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var act = () => harness.Client.DownloadAsync("https://127.0.0.1/setup.exe", true, (_, _) => Task.FromResult(true), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StoreCallException>()).Which.Transient.Should().BeFalse();
        harness.RequestsSource.Should().BeEmpty();
    }

    [Fact]
    public void Gateway_Inspect_Microsoft_ShouldReturnTheFingerprintOnly()
    {
        var gateway = new StoreGateway(null, TimeProvider.System, null);

        var inspection = gateway.Inspect(StoreKind.Microsoft, Secret());

        inspection.IsValid.Should().BeTrue();
        inspection.Fingerprint.TenantIdSuffix.Should().Be("3c07");
        inspection.Fingerprint.ClientIdSuffix.Should().Be("be12");
        inspection.Fingerprint.SellerId.Should().Be(_sellerId);
        JsonSerializer.Serialize(inspection.Fingerprint).Should().NotContain(_clientSecret);
        inspection.NormalizedSecret.Should().Contain(_clientSecret);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"tenantId\":\"x\",\"clientId\":\"0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12\",\"clientSecret\":\"s\",\"sellerId\":\"1\"}")]
    [InlineData("{\"tenantId\":\"8d6b2e5a-1111-4222-8333-9c4e2f2a3c07\",\"clientId\":\"0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12\",\"clientSecret\":\"s\",\"sellerId\":\"abc\"}")]
    [InlineData("{\"tenantId\":\"8d6b2e5a-1111-4222-8333-9c4e2f2a3c07\",\"clientId\":\"0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12\",\"sellerId\":\"1\"}")]
    public void Gateway_Inspect_Microsoft_WithMalformedSecret_ShouldFail(string secret)
    {
        var inspection = new StoreGateway(null, TimeProvider.System, null).Inspect(StoreKind.Microsoft, secret);

        inspection.IsValid.Should().BeFalse();
        inspection.ErrorKey.Should().Be(MessageKey.MsSecretInvalid);
    }

    [Fact]
    public async Task TokenCache_ShouldRefreshShortlyBeforeExpiry()
    {
        var cache = new MicrosoftTokenCache();
        var calls = 0;

        Task<(string, DateTimeOffset)> Acquire() => Task.FromResult(($"t{++calls}", _now.AddMinutes(10)));

        (await cache.GetAsync("k", _now, Acquire, TestContext.Current.CancellationToken)).Should().Be("t1");
        (await cache.GetAsync("k", _now.AddMinutes(4), Acquire, TestContext.Current.CancellationToken)).Should().Be("t1");
        (await cache.GetAsync("k", _now.AddMinutes(6), Acquire, TestContext.Current.CancellationToken)).Should().Be("t2");
    }
}
