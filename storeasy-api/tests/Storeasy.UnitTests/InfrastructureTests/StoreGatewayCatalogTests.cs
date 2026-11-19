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

    [Fact]
    public async Task Apple_IconUrl_ShouldFillTheTemplateOfTheNewestBuildThatHasAnIcon()
    {
        // Arrange
        var (gateway, requests) = CreateGateway(_ => Json("""
            {"data":[
              {"id":"b3","attributes":{"uploadedDate":"2026-11-02T10:00:00Z"}},
              {"id":"b2","attributes":{"uploadedDate":"2026-11-01T10:00:00Z","iconAssetToken":{"templateUrl":"https://is1-ssl.mzstatic.com/image/thumb/Purple/v4/ab/icon.png/{w}x{h}bb.{f}","width":1024,"height":1024}}},
              {"id":"b1","attributes":{"uploadedDate":"2026-10-01T10:00:00Z","iconAssetToken":{"templateUrl":"https://is1-ssl.mzstatic.com/old/{w}x{h}bb.{f}"}}}
            ]}
            """));

        // Act
        var url = await gateway.GetIconUrlAsync(StoreKind.Apple, AppleSecret(), "123", TestContext.Current.CancellationToken);

        // Assert
        url.Should().Be("https://is1-ssl.mzstatic.com/image/thumb/Purple/v4/ab/icon.png/512x512bb.png");
        requests.Should().ContainSingle().Which.Method.Should().Be(HttpMethod.Get);
        var query = Uri.UnescapeDataString(requests[0].RequestUri.Query);
        query.Should().Contain("filter[app]=123").And.Contain("sort=-uploadedDate").And.Contain("fields[builds]=iconAssetToken,uploadedDate");
    }

    [Theory]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{"id":"b1","attributes":{"iconAssetToken":null}}]}""")]
    [InlineData("""{"data":[{"id":"b1","attributes":{"iconAssetToken":{"templateUrl":"http://insecure.example.com/{w}x{h}.{f}"}}}]}""")]
    public void Apple_IconUrl_WithoutAnUsableIcon_ShouldBeNull(string body)
    {
        using var document = JsonDocument.Parse(body);

        StoreGateway.ParseAppleIconUrl(document.RootElement).Should().BeNull();
    }

    [Fact]
    public async Task Google_IconUrl_ShouldReadTheIconOfTheDefaultLanguageInsideAnEditAndDeleteIt()
    {
        // Arrange
        var (gateway, requests) = CreateGateway(request => (request.Method.Method, request.RequestUri.AbsolutePath) switch
        {
            ("POST", "/token") => Json("""{"access_token":"ya29.test"}"""),
            ("POST", var path) when path.EndsWith("/edits") => Json("""{"id":"e1"}"""),
            ("GET", var path) when path.EndsWith("/details") => Json("""{"defaultLanguage":"tr-TR"}"""),
            ("GET", var path) when path.EndsWith("/listings/tr-TR/icon") => Json("""{"images":[{"id":"i1","url":"https://play-lh.googleusercontent.com/abc","sha1":"x"}]}"""),
            ("DELETE", _) => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        // Act
        var url = await gateway.GetIconUrlAsync(StoreKind.Google, GoogleSecret(), "com.example.app", TestContext.Current.CancellationToken);

        // Assert
        url.Should().Be("https://play-lh.googleusercontent.com/abc");
        requests.Should().Contain(r => r.Method == HttpMethod.Delete && r.RequestUri.AbsolutePath.EndsWith("/applications/com.example.app/edits/e1"));
        requests.Where(r => r.RequestUri.AbsolutePath.Contains("/edits/e1")).Should().OnlyContain(r => r.Method == HttpMethod.Get || r.Method == HttpMethod.Delete, "the icon read changes nothing");
    }

    [Fact]
    public void Google_IconUrl_WithoutImages_ShouldBeNull()
    {
        using var document = JsonDocument.Parse("{}");

        StoreGateway.ParseGoogleIconUrl(document.RootElement).Should().BeNull();
    }

    /// <summary>
    /// A display catalog answer recorded from <c>displaycatalog.mp.microsoft.com</c> (trimmed to the fields the parser reads).
    /// </summary>
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfrastructureTests", "Fixtures", name));

    private const string _recordedLogo = "https://store-images.s-microsoft.com/image/apps.25295.14479219485906847.1d173f2c-063b-4bd8-8ff6-d94c5e1d30c5.7cd15392-9e2d-4533-9d7a-51c5d94495b4";

    [Fact]
    public async Task Microsoft_IconUrl_ShouldReadTheLargestLogoFromTheDisplayCatalogWithoutCredentials()
    {
        // Arrange
        var (gateway, requests) = CreateGateway(_ => Json(Fixture("display-catalog-9NZB9KKR92G4.json")));

        // Act
        var url = await gateway.GetIconUrlAsync(StoreKind.Microsoft, "{\"clientSecret\":\"not-sent\"}", "9NZB9KKR92G4", TestContext.Current.CancellationToken);

        // Assert
        url.Should().Be(_recordedLogo, "the 300 px logo is the largest square Logo; the larger BoxArt is only a fallback");
        var request = requests.Should().ContainSingle().Which;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri.Host.Should().Be("displaycatalog.mp.microsoft.com");
        request.RequestUri.AbsolutePath.Should().Be("/v7.0/products");
        request.RequestUri.Query.Should().Contain("bigIds=9NZB9KKR92G4").And.Contain("market=US").And.Contain("languages=en-us");
        request.Headers.Authorization.Should().BeNull("the catalog is public and the connection secret is never sent");
    }

    [Fact]
    public async Task Microsoft_IconUrl_OfAnUnpublishedProduct_ShouldBeNull()
    {
        var (gateway, _) = CreateGateway(_ => Json(Fixture("display-catalog-unpublished.json")));

        var url = await gateway.GetIconUrlAsync(StoreKind.Microsoft, "{}", "9NZZZZZZZZZZ", TestContext.Current.CancellationToken);

        url.Should().BeNull();
    }

    [Fact]
    public async Task Microsoft_IconUrl_NotFound_ShouldBeNull()
    {
        var (gateway, _) = CreateGateway(_ => Json("{}", HttpStatusCode.NotFound));

        var url = await gateway.GetIconUrlAsync(StoreKind.Microsoft, "{}", "9NZB9KKR92G4", TestContext.Current.CancellationToken);

        url.Should().BeNull();
    }

    [Fact]
    public async Task Microsoft_IconUrl_WithoutAProductId_ShouldBeNullWithoutACall()
    {
        var (gateway, requests) = CreateGateway(_ => throw new InvalidOperationException("no network"));

        var url = await gateway.GetIconUrlAsync(StoreKind.Microsoft, "{}", " ", TestContext.Current.CancellationToken);

        url.Should().BeNull();
        requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Microsoft_IconUrl_ServerErrorOrTimeout_ShouldBeTransient()
    {
        var (unavailable, _) = CreateGateway(_ => Json("{}", HttpStatusCode.ServiceUnavailable));
        var (timedOut, _) = CreateGateway(_ => throw new TaskCanceledException("timeout"));

        var unavailableAct = () => unavailable.GetIconUrlAsync(StoreKind.Microsoft, "{}", "9NZB9KKR92G4", TestContext.Current.CancellationToken);
        var timedOutAct = () => timedOut.GetIconUrlAsync(StoreKind.Microsoft, "{}", "9NZB9KKR92G4", TestContext.Current.CancellationToken);

        (await unavailableAct.Should().ThrowAsync<StoreCallException>()).Which.Transient.Should().BeTrue();
        (await timedOutAct.Should().ThrowAsync<StoreCallException>()).Which.Transient.Should().BeTrue();
    }

    [Fact]
    public void Microsoft_IconUrl_ShouldMakeTheProtocolRelativeAddressHttps()
    {
        using var document = JsonDocument.Parse(Fixture("display-catalog-9NZB9KKR92G4.json"));

        var url = StoreGateway.ParseMicrosoftIconUrl(document.RootElement);

        url.Should().StartWith("https://store-images.s-microsoft.com/image/apps.");
    }

    [Theory]
    [InlineData(true, "https://store-images.s-microsoft.com/tile-150")]
    [InlineData(false, "https://store-images.s-microsoft.com/box-2160")]
    public void Microsoft_IconUrl_WithoutALogo_ShouldFallBackToTheLargestSquareTileThenBoxArt(bool withSquareTile, string expected)
    {
        var tile = withSquareTile ? """{"ImagePurpose":"Tile","Width":150,"Height":150,"Uri":"//store-images.s-microsoft.com/tile-150"},""" : string.Empty;
        using var document = JsonDocument.Parse($$"""
            {"Products":[{"LocalizedProperties":[{"Images":[
              {{tile}}
              {"ImagePurpose":"Tile","Width":310,"Height":150,"Uri":"//store-images.s-microsoft.com/wide-tile"},
              {"ImagePurpose":"BoxArt","Width":1080,"Height":1080,"Uri":"//store-images.s-microsoft.com/box-1080"},
              {"ImagePurpose":"BoxArt","Width":2160,"Height":2160,"Uri":"//store-images.s-microsoft.com/box-2160"},
              {"ImagePurpose":"Screenshot","Width":2880,"Height":2880,"Uri":"//store-images.s-microsoft.com/shot"}
            ]}]}]}
            """);

        var url = StoreGateway.ParseMicrosoftIconUrl(document.RootElement);

        url.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"Products":[]}""")]
    [InlineData("""{}""")]
    [InlineData("""{"Products":[{"LocalizedProperties":[]}]}""")]
    [InlineData("""{"Products":[{"LocalizedProperties":[{"Images":[{"ImagePurpose":"Screenshot","Width":300,"Height":300,"Uri":"//store-images.s-microsoft.com/shot"}]}]}]}""")]
    [InlineData("""{"Products":[{"LocalizedProperties":[{"Images":[{"ImagePurpose":"Logo","Width":300,"Height":150,"Uri":"//store-images.s-microsoft.com/wide"}]}]}]}""")]
    [InlineData("""{"Products":[{"LocalizedProperties":[{"Images":[{"ImagePurpose":"Logo","Width":300,"Height":300,"Uri":"http://insecure.example.com/logo"}]}]}]}""")]
    [InlineData("""{"Products":[{"LocalizedProperties":[{"Images":[{"ImagePurpose":"Logo","Width":300,"Height":300}]}]}]}""")]
    public void Microsoft_IconUrl_WithoutAnUsableLogo_ShouldBeNull(string body)
    {
        using var document = JsonDocument.Parse(body);

        StoreGateway.ParseMicrosoftIconUrl(document.RootElement).Should().BeNull();
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
