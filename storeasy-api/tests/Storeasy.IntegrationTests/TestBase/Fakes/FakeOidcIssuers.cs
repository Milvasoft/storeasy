using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// CI providers' OIDC issuers as the token validator sees them: every issuer host answers its discovery document and a key
/// set with one locally generated RSA key. Tokens are signed with <see cref="CreateToken"/>; an unknown key signs a token
/// that must fail. Counts key set downloads, so the cache can be checked.
/// </summary>
public sealed class FakeOidcIssuers : HttpMessageHandler
{
    /// <summary>
    /// GitHub Actions issuer.
    /// </summary>
    public const string GitHub = "https://token.actions.githubusercontent.com";

    /// <summary>
    /// An Azure DevOps organization issuer.
    /// </summary>
    public static readonly string Azure = "https://vstoken.dev.azure.com/" + Guid.Parse("8f5a1c0e-4b7d-4e2a-9c3b-1d2e3f4a5b6c");

    private readonly RSA _key = RSA.Create(2048);
    private readonly RSA _foreignKey = RSA.Create(2048);
    private int _keySetDownloads;

    /// <summary>
    /// Key id of the issuers' key.
    /// </summary>
    public string KeyId { get; } = "test-key-1";

    /// <summary>
    /// How many key sets were downloaded.
    /// </summary>
    public int KeySetDownloads => _keySetDownloads;

    /// <summary>
    /// Signs a token. <paramref name="foreignKey"/> signs with a key no issuer publishes.
    /// </summary>
    public string CreateToken(string issuer, string audience, Dictionary<string, object> claims, TimeSpan? lifetime = null, bool foreignKey = false)
    {
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(5));
        var key = new RsaSecurityKey(foreignKey ? _foreignKey : _key) { KeyId = KeyId };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expires < now ? expires.AddMinutes(-10) : now,
            NotBefore = expires < now ? expires.AddMinutes(-10) : now.AddSeconds(-5),
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        var origin = $"{uri.Scheme}://{uri.Host}";
        var path = uri.AbsolutePath;

        if (path.EndsWith("/.well-known/openid-configuration", StringComparison.Ordinal))
        {
            var issuer = origin + path[..^"/.well-known/openid-configuration".Length];

            return Json(new JsonObject { ["issuer"] = issuer, ["jwks_uri"] = issuer + "/.well-known/jwks" });
        }

        if (path.EndsWith("/.well-known/jwks", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _keySetDownloads);

            var parameters = _key.ExportParameters(false);

            return Json(new JsonObject
            {
                ["keys"] = new JsonArray(new JsonObject
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = KeyId,
                    ["n"] = Base64UrlEncoder.Encode(parameters.Modulus),
                    ["e"] = Base64UrlEncoder.Encode(parameters.Exponent),
                }),
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _key.Dispose();
            _foreignKey.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Task<HttpResponseMessage> Json(JsonNode body)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") });

    /// <summary>
    /// GitHub Actions claims of a run of <paramref name="repository"/> on <paramref name="branch"/>.
    /// </summary>
    public static Dictionary<string, object> GitHubClaims(string repository, string branch = "main", string environment = null) => new()
    {
        ["repository"] = repository,
        ["ref"] = "refs/heads/" + branch,
        ["sub"] = environment == null ? $"repo:{repository}:ref:refs/heads/{branch}" : $"repo:{repository}:environment:{environment}",
        ["environment"] = environment,
        ["run_id"] = "318",
        ["job_workflow_ref"] = $"{repository}/.github/workflows/release.yml@refs/heads/{branch}",
    };

    /// <summary>
    /// Serializes claims for a debugging message.
    /// </summary>
    public static string Describe(Dictionary<string, object> claims) => JsonSerializer.Serialize(claims);
}
