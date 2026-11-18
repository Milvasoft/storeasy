using FluentAssertions;
using Milvasoft.Core.Abstractions;
using Moq;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Store gateway unit tests (no network).")]
public class StoreGatewayTests
{
    private const string _issuerId = "57246542-96fe-1a63-e053-0824d011072a";
    private const string _keyId = "ABC123DEFG";

    private static readonly DateTimeOffset _now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private static string AppleSecret(string pem = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return JsonSerializer.Serialize(new { issuerId = _issuerId, keyId = _keyId, privateKey = pem ?? key.ExportPkcs8PrivateKeyPem() });
    }

    private static string GoogleSecret(string tokenUri = "https://oauth2.googleapis.com/token")
    {
        using var key = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "demo-project",
            ["private_key_id"] = "kid1",
            ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "store@demo-project.iam.gserviceaccount.com",
            ["token_uri"] = tokenUri,
        });
    }

    private static (StoreGateway Gateway, List<HttpRequestMessage> Requests) CreateGateway(Func<HttpRequestMessage, HttpResponseMessage> respond) => CreateGateway(respond, []);

    private static (StoreGateway Gateway, List<HttpRequestMessage> Requests) CreateGateway(Func<HttpRequestMessage, HttpResponseMessage> respond, List<string> bodies)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHandler(request =>
        {
            requests.Add(request);
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return respond(request);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(StoreGateway.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false));

        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(_now);

        return (new StoreGateway(factory.Object, time.Object, new Mock<IMilvaLogger>().Object), requests);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public void Inspect_Apple_ShouldReturnFingerprintWithoutSecret()
    {
        var (gateway, _) = CreateGateway(_ => throw new InvalidOperationException("no network"));

        var result = gateway.Inspect(StoreKind.Apple, AppleSecret());

        result.IsValid.Should().BeTrue();
        result.Fingerprint.KeyId.Should().Be(_keyId);
        result.Fingerprint.IssuerIdSuffix.Should().Be("072a");
        JsonSerializer.Serialize(result.Fingerprint).Should().NotContain("PRIVATE");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"issuerId\":\"x\",\"keyId\":\"ABC123DEFG\",\"privateKey\":\"k\"}")]
    [InlineData("{\"issuerId\":\"57246542-96fe-1a63-e053-0824d011072a\",\"keyId\":\"short\",\"privateKey\":\"k\"}")]
    public void Inspect_Apple_WithMalformedSecret_ShouldFail(string secret)
    {
        var (gateway, _) = CreateGateway(_ => throw new InvalidOperationException("no network"));

        var result = gateway.Inspect(StoreKind.Apple, secret);

        result.IsValid.Should().BeFalse();
        result.ErrorKey.Should().Be(StoreCredentialParser.AppleSecretInvalid);
    }

    [Fact]
    public void Inspect_Apple_WithRsaKey_ShouldRejectTheKey()
    {
        using var rsa = RSA.Create(2048);
        var (gateway, _) = CreateGateway(_ => throw new InvalidOperationException("no network"));

        var result = gateway.Inspect(StoreKind.Apple, AppleSecret(rsa.ExportPkcs8PrivateKeyPem()));

        result.IsValid.Should().BeFalse();
        result.ErrorKey.Should().Be(StoreCredentialParser.AppleKeyInvalid);
    }

    [Fact]
    public void Inspect_Google_WithForeignTokenUri_ShouldFail()
    {
        var (gateway, _) = CreateGateway(_ => throw new InvalidOperationException("no network"));

        gateway.Inspect(StoreKind.Google, GoogleSecret("https://attacker.example/token")).IsValid.Should().BeFalse();
        gateway.Inspect(StoreKind.Google, GoogleSecret()).Fingerprint.ServiceAccountEmail.Should().Be("store@demo-project.iam.gserviceaccount.com");
    }

    [Fact]
    public void AppleToken_ShouldCarryTheAppStoreConnectClaims()
    {
        var credentials = StoreCredentialParser.ParseApple(AppleSecret(), out _);

        var token = StoreJwt.CreateAppleToken(credentials, _now);

        var header = StoreJwt.DecodeHeader(token);
        var payload = StoreJwt.DecodePayload(token);

        header.GetProperty("alg").GetString().Should().Be("ES256");
        header.GetProperty("kid").GetString().Should().Be(_keyId);
        payload.GetProperty("iss").GetString().Should().Be(_issuerId);
        payload.GetProperty("aud").GetString().Should().Be("appstoreconnect-v1");
        (payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64()).Should().Be(1140);
        Convert.FromBase64String(PadBase64(token.Split('.')[2])).Should().HaveCount(64);
    }

    [Fact]
    public void AppleToken_SignatureShouldVerifyWithThePublicKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentials = StoreCredentialParser.ParseApple(AppleSecret(key.ExportPkcs8PrivateKeyPem()), out _);

        var parts = StoreJwt.CreateAppleToken(credentials, _now).Split('.');

        key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Convert.FromBase64String(PadBase64(parts[2])), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
           .Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_Apple_ShouldCallAppsWithBearer()
    {
        var (gateway, requests) = CreateGateway(_ => Json(HttpStatusCode.OK, "{\"data\":[]}"));

        var result = await gateway.VerifyAsync(StoreKind.Apple, AppleSecret(), null, CancellationToken.None);

        result.Status.Should().Be(ConnectionStatus.Verified);
        requests.Should().ContainSingle();
        requests[0].RequestUri!.AbsolutePath.Should().Be("/v1/apps");
        requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task VerifyAsync_Apple_401_ShouldBeRejectedWithRedactedDetail()
    {
        var (gateway, _) = CreateGateway(_ => Json(HttpStatusCode.Unauthorized, "{\"errors\":[{\"status\":\"401\",\"detail\":\"Provide a properly configured and signed bearer token\"}]}"));

        var result = await gateway.VerifyAsync(StoreKind.Apple, AppleSecret(), null, CancellationToken.None);

        result.Status.Should().Be(ConnectionStatus.Rejected);
        result.Error.Should().Contain("401").And.Contain("Provide a properly configured");
        result.Error.Should().NotContain("Bearer ey");
    }

    [Fact]
    public async Task VerifyAsync_Apple_503_ShouldBeUnreachable()
    {
        var (gateway, _) = CreateGateway(_ => Json(HttpStatusCode.ServiceUnavailable, "{}"));

        var result = await gateway.VerifyAsync(StoreKind.Apple, AppleSecret(), null, CancellationToken.None);

        result.Status.Should().Be(ConnectionStatus.Unreachable);
    }

    [Fact]
    public async Task VerifyAsync_Google_WithPackage_ShouldExchangeTokenThenProbeAndDeleteAnEdit()
    {
        var bodies = new List<string>();
        var (gateway, requests) = CreateGateway(request => request.RequestUri!.Host switch
        {
            "oauth2.googleapis.com" => Json(HttpStatusCode.OK, "{\"access_token\":\"ya29.test\",\"expires_in\":3600}"),
            _ when request.Method == HttpMethod.Post => Json(HttpStatusCode.OK, "{\"id\":\"edit-1\"}"),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
        }, bodies);

        var result = await gateway.VerifyAsync(StoreKind.Google, GoogleSecret(), "com.example.app", CancellationToken.None);

        result.Status.Should().Be(ConnectionStatus.Verified);
        requests.Select(r => $"{r.Method} {r.RequestUri!.AbsolutePath}").Should().Equal(
            "POST /token",
            "POST /androidpublisher/v3/applications/com.example.app/edits",
            "DELETE /androidpublisher/v3/applications/com.example.app/edits/edit-1");
        bodies[0].Should().Contain("grant_type=urn").And.Contain("assertion=");
    }

    [Fact]
    public async Task VerifyAsync_Google_AppNotShared_ShouldBeRejected()
    {
        var (gateway, _) = CreateGateway(request => request.RequestUri!.Host == "oauth2.googleapis.com"
            ? Json(HttpStatusCode.OK, "{\"access_token\":\"ya29.test\"}")
            : Json(HttpStatusCode.Forbidden, "{\"error\":{\"code\":403,\"message\":\"The caller does not have permission\"}}"));

        var result = await gateway.VerifyAsync(StoreKind.Google, GoogleSecret(), "com.example.app", CancellationToken.None);

        result.Status.Should().Be(ConnectionStatus.Rejected);
        result.Error.Should().Contain("The caller does not have permission");
    }

    private static string PadBase64(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        return padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
                await request.Content.LoadIntoBufferAsync(cancellationToken);

            return respond(request);
        }
    }
}
