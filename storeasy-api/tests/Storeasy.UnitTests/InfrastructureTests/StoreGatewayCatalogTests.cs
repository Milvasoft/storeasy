using FluentAssertions;
using Milvasoft.Core.Abstractions;
using Moq;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using Storeasy.UnitTests.TestSupport;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Store gateway app and build reads (fake HTTP, no network).")]
public class StoreGatewayCatalogTests
{
    private static string AppleSecret()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId = "ABC123DEFG", privateKey = key.ExportPkcs8PrivateKeyPem() });
    }

    private static string GoogleSecret()
    {
        using var key = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "demo",
            ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "store@demo.iam.gserviceaccount.com",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
        });
    }

    private static (StoreGateway Gateway, List<HttpRequestMessage> Requests) CreateGateway(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHandler(request =>
        {
            requests.Add(request);
            return respond(request);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(StoreGateway.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));

        return (new StoreGateway(factory.Object, TimeProvider.System, new Mock<IMilvaLogger>().Object), requests);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Apple_ListApps_ShouldFollowTheNextLinks()
    {
        // Arrange
        var (gateway, requests) = CreateGateway(request => request.RequestUri.ToString().Contains("cursor=2")
            ? Json("""{"data":[{"id":"2","attributes":{"name":"Two","bundleId":"com.example.two"}}],"links":{}}""")
            : Json("""{"data":[{"id":"1","attributes":{"name":"One","bundleId":"com.example.one"}}],"links":{"next":"https://api.appstoreconnect.apple.com/v1/apps?cursor=2"}}"""));

        // Act
        var apps = await gateway.ListAppsAsync(StoreKind.Apple, AppleSecret(), TestContext.Current.CancellationToken);

        // Assert
        apps.Should().Equal(new StoreAppRecord("1", "com.example.one", "One"), new StoreAppRecord("2", "com.example.two", "Two"));
        requests.Should().HaveCount(2);
        requests[0].Headers.Authorization.Scheme.Should().Be("Bearer");
    }

    [Fact]
    public void Apple_Builds_ShouldTakeTheVersionFromThePreReleaseVersionAndMapTheState()
    {
        // Arrange
        using var document = JsonDocument.Parse("""
            {
              "data": [
                {"id":"b1","attributes":{"version":"45","uploadedDate":"2026-11-01T10:00:00-08:00","expirationDate":"2027-01-30T10:00:00-08:00","expired":false,"processingState":"VALID","minOsVersion":"17.0"},
                 "relationships":{"preReleaseVersion":{"data":{"type":"preReleaseVersions","id":"v1"}}}},
                {"id":"b0","attributes":{"version":"44","expired":true,"processingState":"VALID"},
                 "relationships":{"preReleaseVersion":{"data":{"type":"preReleaseVersions","id":"v1"}}}},
                {"id":"b2","attributes":{"version":"46","processingState":"PROCESSING"}}
              ],
              "included": [{"type":"preReleaseVersions","id":"v1","attributes":{"version":"1.4.0","platform":"IOS"}}]
            }
            """);

        // Act
        var builds = StoreGateway.ParseAppleBuilds(document.RootElement);

        // Assert
        builds[0].Should().Be(new StoreBuildRecord("b1", "1.4.0", "45", BuildProcessingState.Valid, new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 30, 18, 0, 0, DateTimeKind.Utc), null, "17.0"));
        builds[1].ProcessingState.Should().Be(BuildProcessingState.Expired);
        builds[2].Version.Should().BeNull();
        builds[2].ProcessingState.Should().Be(BuildProcessingState.Processing);
    }

    [Fact]
    public async Task Google_ListBuilds_ShouldReadBundlesAndTracksInsideAnEditAndDeleteIt()
    {
        // Arrange
        var (gateway, requests) = CreateGateway(request => (request.Method.Method, request.RequestUri.AbsolutePath) switch
        {
            ("POST", "/token") => Json("""{"access_token":"ya29.test"}"""),
            ("POST", var path) when path.EndsWith("/edits") => Json("""{"id":"e1"}"""),
            ("GET", var path) when path.EndsWith("/bundles") => Json("""{"bundles":[{"versionCode":12},{"versionCode":13}]}"""),
            ("GET", var path) when path.EndsWith("/tracks") => Json("""{"tracks":[{"track":"internal","releases":[{"name":"1.3.0","versionCodes":["13"]}]},{"track":"production","releases":[{"name":"1.2.0","versionCodes":["12"]}]}]}"""),
            ("DELETE", _) => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        // Act
        var builds = await gateway.ListBuildsAsync(StoreKind.Google, GoogleSecret(), "com.example.app", TestContext.Current.CancellationToken);

        // Assert
        builds.Select(b => (b.ExternalId, b.Version, b.Tracks)).Should().Equal(("13", "1.3.0", "internal"), ("12", "1.2.0", "production"));
        builds.Should().OnlyContain(b => b.ProcessingState == BuildProcessingState.Valid);
        requests.Should().Contain(r => r.Method == HttpMethod.Delete && r.RequestUri.AbsolutePath.EndsWith("/applications/com.example.app/edits/e1"));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task StoreErrors_ShouldBeTransientOnlyForRateLimitsAndServerErrors(HttpStatusCode status, bool transient)
    {
        var (gateway, _) = CreateGateway(_ => Json("""{"errors":[{"detail":"nope"}]}""", status));

        var act = () => gateway.ListAppsAsync(StoreKind.Apple, AppleSecret(), TestContext.Current.CancellationToken);

        var error = (await act.Should().ThrowAsync<StoreCallException>()).Which;
        error.Transient.Should().Be(transient);
        error.StatusCode.Should().Be((int)status);
        error.Message.Should().Contain("nope");
    }

    [Fact]
    public async Task NetworkFailure_ShouldBeTransient()
    {
        var (gateway, _) = CreateGateway(_ => throw new HttpRequestException("connection reset"));

        var act = () => gateway.GetAppAsync(StoreKind.Apple, AppleSecret(), "1", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StoreCallException>()).Which.Transient.Should().BeTrue();
    }

    [Theory]
    [InlineData(StoreKind.Google)]
    [InlineData(StoreKind.Microsoft)]
    public async Task ListApps_WithoutAListApi_ShouldFailPermanentlyWithoutACall(StoreKind store)
    {
        var (gateway, requests) = CreateGateway(_ => throw new InvalidOperationException("no network"));

        var act = () => gateway.ListAppsAsync(store, "{}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<StoreCallException>()).Which.Transient.Should().BeFalse();
        requests.Should().BeEmpty();
    }
}
