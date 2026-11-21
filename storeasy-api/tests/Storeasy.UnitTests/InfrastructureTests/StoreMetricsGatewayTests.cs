using FluentAssertions;
using Moq;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.StoreClients;
using Storeasy.Infrastructure.StoreMetrics;
using Storeasy.Infrastructure.Stores;
using Storeasy.UnitTests.TestSupport;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Store page metrics reads against fake HTTP: App Store analytics reports, the Play statistics bucket, Microsoft analytics.")]
public class StoreMetricsGatewayTests
{
    private const string _segmentUrl = "https://reports.example.test/segment-1.gz";

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

    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "UtilsTests", "Fixtures", "StoreMetrics", name));

    private static (StoreMetricsGateway Gateway, List<(HttpRequestMessage Request, string Body)> Requests) Create(Func<HttpRequestMessage, HttpResponseMessage> respond, IMicrosoftStoreClient microsoft = null)
    {
        var requests = new List<(HttpRequestMessage, string)>();
        var handler = new FakeHandler(request =>
        {
            requests.Add((request, request.Content?.ReadAsStringAsync().Result));
            return respond(request);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(StoreGateway.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));

        return (new StoreMetricsGateway(new StoreApiClient(factory.Object, TimeProvider.System), microsoft ?? Mock.Of<IMicrosoftStoreClient>()), requests);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(byte[] body) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    [Fact]
    public async Task ReadAppleMetrics_ShouldDownloadTheNewInstancesWithoutSendingTheTokenToTheSegmentHost()
    {
        // Arrange: a downloads report with an old and a new instance, no engagement report yet.
        var (gateway, requests) = Create(request => request.RequestUri.AbsolutePath switch
        {
            "/v1/analyticsReportRequests/rr-1/reports" => Json("""{"data":[{"type":"analyticsReports","id":"r-dl","attributes":{"name":"App Downloads Standard","category":"COMMERCE"}},{"type":"analyticsReports","id":"r-x","attributes":{"name":"App Crashes","category":"APP_USAGE"}}]}"""),
            "/v1/analyticsReports/r-dl/instances" => Json("""{"data":[{"id":"i-old","attributes":{"granularity":"DAILY","processingDate":"2026-11-01"}},{"id":"i-new","attributes":{"granularity":"DAILY","processingDate":"2026-11-19"}}]}"""),
            "/v1/analyticsReportInstances/i-new/segments" => Json($$$"""{"data":[{"id":"s-1","attributes":{"url":"{{{_segmentUrl}}}","checksum":"x","sizeInBytes":277}}]}"""),
            "/segment-1.gz" => Bytes(Fixture("apple_downloads_standard.tsv.gz")),
            _ => Json("""{"errors":[{"detail":"unexpected"}]}""", HttpStatusCode.NotFound),
        });

        // Act
        var batch = await gateway.ReadAppleMetricsAsync(AppleSecret(), "rr-1", new DateOnly(2026, 11, 10), TestContext.Current.CancellationToken);

        // Assert
        batch.ReportsReady.Should().BeTrue();
        batch.Marker.Should().Be(new DateOnly(2026, 11, 19));
        batch.Rows.Single(r => r.Country == "" && r.Source == TrafficSource.All).Should().Match<StoreMetricRow>(r => r.Downloads == 46 && r.Redownloads == 5);
        requests.Should().NotContain(r => r.Request.RequestUri.AbsolutePath.Contains("i-old"), "instances processed before the marker window are skipped");
        requests.Single(r => r.Request.RequestUri.Host == "reports.example.test").Request.Headers.Authorization.Should().BeNull();
        requests.Where(r => r.Request.RequestUri.Host == "api.appstoreconnect.apple.com").Should().OnlyContain(r => r.Request.Headers.Authorization.Scheme == "Bearer");
    }

    [Fact]
    public async Task ReadAppleMetrics_ShouldSayNotReadyBeforeAppleGeneratedTheReports()
    {
        // Arrange
        var (gateway, _) = Create(_ => Json("""{"data":[]}"""));

        // Act
        var batch = await gateway.ReadAppleMetricsAsync(AppleSecret(), "rr-1", null, TestContext.Current.CancellationToken);

        // Assert
        batch.ReportsReady.Should().BeFalse();
        batch.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAppleReportRequest_ShouldReuseAnActiveRequestAndReplaceAStoppedOne()
    {
        // Arrange
        var posted = new List<string>();
        var stopped = true;
        var (gateway, _) = Create(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                posted.Add(request.Content.ReadAsStringAsync().Result);
                return Json("""{"data":{"type":"analyticsReportRequests","id":"rr-new"}}""", HttpStatusCode.Created);
            }

            return Json($$$"""{"data":[{"type":"analyticsReportRequests","id":"rr-old","attributes":{"accessType":"ONGOING","stoppedDueToInactivity":{{{(stopped ? "true" : "false")}}}}}]}""");
        });

        // Act
        var replaced = await gateway.CreateAppleReportRequestAsync(AppleSecret(), "6400000001", TestContext.Current.CancellationToken);
        stopped = false;
        var reused = await gateway.CreateAppleReportRequestAsync(AppleSecret(), "6400000001", TestContext.Current.CancellationToken);

        // Assert
        replaced.Should().Be(new AppleReportRequest("rr-new", false));
        reused.Should().Be(new AppleReportRequest("rr-old", false));
        posted.Should().ContainSingle().Which.Should().Contain("\"accessType\":\"ONGOING\"").And.Contain("\"id\":\"6400000001\"");
    }

    [Fact]
    public async Task ReadPlayMetrics_ShouldReadTheBucketWithTheStorageScopeAndSkipMissingMonths()
    {
        // Arrange
        var (gateway, requests) = Create(request =>
        {
            if (request.RequestUri.Host == "oauth2.googleapis.com")
                return Json("""{"access_token":"ya29.storage","expires_in":3600}""");

            var name = Uri.UnescapeDataString(request.RequestUri.AbsolutePath.Split("/o/")[1]);

            return name.EndsWith("_202610_country.csv", StringComparison.Ordinal) ? Bytes(Fixture("store_performance_com.example.pusula_202610_country.csv"))
                 : name.EndsWith("_202610_traffic_source.csv", StringComparison.Ordinal) ? Bytes(Fixture("store_performance_com.example.pusula_202610_traffic_source.csv"))
                 : Json("""{"error":{"code":404,"message":"No such object"}}""", HttpStatusCode.NotFound);
        });

        // Act
        var batch = await gateway.ReadPlayMetricsAsync(GoogleSecret(), "pubsite_prod_rev_123", "com.example.pusula", new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 21), TestContext.Current.CancellationToken);

        // Assert
        batch.Rows.Single(r => r.Date == new DateOnly(2026, 10, 1) && r.Country == "" && r.Source == TrafficSource.All).Should().Match<StoreMetricRow>(r => r.PageViews == 140 && r.Downloads == 15);
        var token = requests.Single(r => r.Request.RequestUri.Host == "oauth2.googleapis.com").Body;
        var assertion = token.Split("assertion=")[1];
        Encoding.UTF8.GetString(Base64Url(assertion.Split('.')[1])).Should().Contain("devstorage.read_only");
        requests.Where(r => r.Request.RequestUri.Host == "storage.googleapis.com").Select(r => r.Request.RequestUri.AbsoluteUri).Should().HaveCount(4).And.OnlyContain(u => u.Contains("/b/pubsite_prod_rev_123/o/stats%2Fstore_performance%2F") && u.EndsWith("?alt=media"));
    }

    [Fact]
    public async Task ReadPlayMetrics_ShouldSurfaceARefusedBucketAsA403()
    {
        // Arrange
        var (gateway, _) = Create(request => request.RequestUri.Host == "oauth2.googleapis.com"
            ? Json("""{"access_token":"ya29.storage","expires_in":3600}""")
            : Json("""{"error":{"code":403,"message":"s@demo.iam.gserviceaccount.com does not have storage.objects.get access"}}""", HttpStatusCode.Forbidden));

        // Act
        var act = () => gateway.ReadPlayMetricsAsync(GoogleSecret(), "pubsite_prod_rev_123", "com.example.pusula", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 21), TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<StoreCallException>()).Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ReadMicrosoftMetrics_ShouldFollowTheNextLinkAndReadTheFunnelPerDay()
    {
        // Arrange
        var paths = new List<string>();
        var microsoft = new Mock<IMicrosoftStoreClient>();
        microsoft.Setup(m => m.GetAnalyticsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((string _, string path, CancellationToken _) =>
                 {
                     paths.Add(path);

                     if (path.Contains("funnel", StringComparison.Ordinal))
                         return JsonNode.Parse(Fixture("ms_funnel.json"));

                     return path.Contains("skip=", StringComparison.Ordinal) ? JsonNode.Parse("""{"Value":[],"TotalCount":0}""") : JsonNode.Parse(Fixture("ms_appacquisitions.json"));
                 });
        var (gateway, _) = Create(_ => throw new InvalidOperationException("no HTTP"), microsoft.Object);

        // Act
        var batch = await gateway.ReadMicrosoftMetricsAsync("{}", "9NBLGGGZ5QDR", new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 19), TestContext.Current.CancellationToken);

        // Assert
        paths[0].Should().Be("analytics/appacquisitions?applicationId=9NBLGGGZ5QDR&startDate=2026-11-18&endDate=2026-11-19&aggregationLevel=day&groupby=date,market,storeClient&top=10000");
        paths[1].Should().Contain("skip=10000");
        paths.Count(p => p.StartsWith("analytics/funnel?", StringComparison.Ordinal)).Should().Be(2);
        batch.Rows.Single(r => r.Date == new DateOnly(2026, 11, 19) && r.Country == "" && r.Source == TrafficSource.All).Should().Match<StoreMetricRow>(r => r.Downloads == 4 && r.PageViews == 40);
    }

    [Fact]
    public void Inflate_ShouldReadGzipAndPlainText()
    {
        StoreMetricsGateway.Inflate(Fixture("apple_downloads_standard.tsv.gz")).Should().StartWith("Date\tApp Name");
        StoreMetricsGateway.Inflate(Encoding.UTF8.GetBytes("Date\tCounts\n")).Should().Be("Date\tCounts\n");
    }

    private static byte[] Base64Url(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '='));
    }
}
