using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Storeasy.Api.Mcp;
using Storeasy.Domain.Intake;
using Storeasy.Infrastructure.Intake;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.IntakeTests;

[Trait("Intake Unit Tests", "OIDC token validation against an issuer's key set, and the remote MCP tool catalog.")]
public class OidcAndMcpTests
{
    private sealed class Issuer : HttpMessageHandler
    {
        public RSA Key { get; } = RSA.Create(2048);

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;

            var path = request.RequestUri.AbsolutePath;
            var origin = $"https://{request.RequestUri.Host}";
            var parameters = Key.ExportParameters(false);

            JsonNode body = path.EndsWith("openid-configuration", StringComparison.Ordinal)
                ? new JsonObject { ["jwks_uri"] = origin + "/.well-known/jwks" }
                : new JsonObject { ["keys"] = new JsonArray(new JsonObject { ["kty"] = "RSA", ["kid"] = "k1", ["n"] = Base64UrlEncoder.Encode(parameters.Modulus), ["e"] = Base64UrlEncoder.Encode(parameters.Exponent) }) };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") });
        }

        public string Token(string issuer, DateTime expires, RSA key = null) => new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = "storeasy",
            Expires = expires,
            IssuedAt = expires.AddMinutes(-10),
            NotBefore = expires.AddMinutes(-10),
            Claims = new Dictionary<string, object> { ["repository"] = "kuzeylab/halka" },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key ?? Key) { KeyId = "k1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private static (OidcTokenValidator Validator, Issuer Issuer) Create()
    {
        var issuer = new Issuer();
        var factory = new Mock<IHttpClientFactory>();

        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(issuer, disposeHandler: false));

        return (new OidcTokenValidator(factory.Object, TimeProvider.System), issuer);
    }

    [Fact]
    public async Task ValidToken_ShouldPassAndTheKeySetShouldBeCached()
    {
        var (validator, issuer) = Create();

        var first = await validator.ValidateAsync(issuer.Token(TrustedPublisherMatcher.GitHubIssuer, DateTime.UtcNow.AddMinutes(5)), [], CancellationToken.None);
        var requests = issuer.Requests;
        var second = await validator.ValidateAsync(issuer.Token(TrustedPublisherMatcher.GitHubIssuer, DateTime.UtcNow.AddMinutes(5)), [], CancellationToken.None);

        first.Valid.Should().BeTrue(first.Error);
        first.Audiences.Should().Equal("storeasy");
        first.Claims["repository"].Should().Be("kuzeylab/halka");
        second.Valid.Should().BeTrue();
        issuer.Requests.Should().Be(requests, "the key set is cached");
    }

    [Fact]
    public async Task ExpiredForeignOrUnlistedTokens_ShouldFail()
    {
        var (validator, issuer) = Create();
        using var foreign = RSA.Create(2048);

        (await validator.ValidateAsync(issuer.Token(TrustedPublisherMatcher.GitHubIssuer, DateTime.UtcNow.AddMinutes(-10)), [], CancellationToken.None)).Valid.Should().BeFalse();
        (await validator.ValidateAsync(issuer.Token(TrustedPublisherMatcher.GitHubIssuer, DateTime.UtcNow.AddMinutes(5), foreign), [], CancellationToken.None)).Valid.Should().BeFalse();
        (await validator.ValidateAsync(issuer.Token("https://evil.example.com", DateTime.UtcNow.AddMinutes(5)), [], CancellationToken.None)).Valid.Should().BeFalse();
        (await validator.ValidateAsync(issuer.Token("https://git.kuzeylab.dev", DateTime.UtcNow.AddMinutes(5)), ["https://git.kuzeylab.dev"], CancellationToken.None)).Valid.Should().BeTrue();
        (await validator.ValidateAsync("not-a-jwt", [], CancellationToken.None)).Valid.Should().BeFalse();
        issuer.Requests.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("https://token.actions.githubusercontent.com", true)]
    [InlineData("https://gitlab.com", true)]
    [InlineData("https://vstoken.dev.azure.com/8f5a1c0e-4b7d-4e2a-9c3b-1d2e3f4a5b6c", true)]
    [InlineData("http://token.actions.githubusercontent.com", false)]
    [InlineData("https://vstoken.dev.azure.com/whatever", false)]
    [InlineData("https://example.com", false)]
    public void Issuers_ShouldBeAllowListed(string issuer, bool allowed) => OidcTokenValidator.IsAllowedIssuer(issuer, []).Should().Be(allowed);

    [Fact]
    public void Catalog_ShouldGiveEveryToolAValidSchemaWithoutTheOrganization()
    {
        var names = McpToolCatalog.Tools.Select(t => t.Name).ToList();

        names.Should().OnlyHaveUniqueItems();
        names.Should().NotContain("artifact_upload");
        names.Should().OnlyContain(n => System.Text.RegularExpressions.Regex.IsMatch(n, "^[a-z][a-z0-9_]{2,63}$"));

        foreach (var tool in McpToolCatalog.Tools)
        {
            var schema = McpSchemaBuilder.Build(tool);

            schema["type"]!.GetValue<string>().Should().Be("object", tool.Name);
            (schema["properties"] as JsonObject)?.ContainsKey("organizationId").Should().NotBe(true, tool.Name);
            tool.Description.Should().NotBeNullOrWhiteSpace();
            tool.Description.Should().NotContain(((char)0x2014).ToString()).And.NotContain(((char)0x2013).ToString());

            if (tool.Kind == McpToolKind.Plan && tool.RequestType != null)
                tool.Fixed.Should().ContainKey("DryRun", $"{tool.Name} must stay a dry run");
        }
    }

    [Fact]
    public void FileArguments_ShouldCarryTheUploadKeyword()
    {
        var folder = McpSchemaBuilder.Build(McpToolCatalog.Find("screenshots_upload_dir"));
        var files = McpSchemaBuilder.Build(McpToolCatalog.Find("screenshots_plan"));

        folder["properties"]!["screenshots"]![McpSchemaBuilder.UploadKeyword]!["kind"]!.GetValue<string>().Should().Be("folder");
        folder["properties"]!["screenshots"]![McpSchemaBuilder.UploadKeyword]!["purpose"]!.GetValue<int>().Should().Be(1);
        files["properties"]!["assetIds"]![McpSchemaBuilder.UploadKeyword]!["kind"]!.GetValue<string>().Should().Be("files");
        files["properties"]!["kind"].Should().BeNull("the media kind is fixed by the tool");
        JsonSerializer.Serialize(files).Should().NotContain("\"dryRun\"");
    }

    [Fact]
    public void Requests_ShouldBeBuiltFromArgumentsWithTheFixedValues()
    {
        var tool = McpToolCatalog.Find("listings_plan");
        var arguments = JsonNode.Parse("""{"storeAppId":5,"organizationId":99,"dryRun":false,"changes":[{"locale":"en-US","field":"title","value":"x"}]}""")!.AsObject();

        var request = (Storeasy.Application.Features.Listings.PlanListing.PlanListingCommand)McpToolInvoker.CreateRequest(tool, arguments);

        request.StoreAppId.Should().Be(5);
        request.OrganizationId.Should().Be(0, "the organization comes from the token");
        request.DryRun.Should().BeTrue();
    }
}
