using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Google's OpenID Connect endpoints as the sign in sees them, at <see cref="Authority"/>: the discovery document, a key
/// set with one locally generated RSA key and the token endpoint. A test plays the browser and Google's consent page with
/// <see cref="Authorize"/>: it takes the authorization address storeasy produced and returns the code Google would send
/// back. The token endpoint checks the client, the redirect address and the PKCE verifier like Google does, and signs an
/// id token with the nonce of that authorization.
/// </summary>
public sealed class FakeGoogle : HttpMessageHandler
{
    /// <summary>
    /// Issuer the test hosts are configured with.
    /// </summary>
    public const string Authority = "https://accounts.google.test";

    /// <summary>
    /// OAuth client of the test hosts.
    /// </summary>
    public const string ClientId = "storeasy-tests.apps.googleusercontent.test";

    /// <summary>
    /// OAuth client secret of the test hosts (test only).
    /// </summary>
    public const string ClientSecret = "google-client-secret-for-tests";

    private readonly RSA _key = RSA.Create(2048);
    private readonly ConcurrentDictionary<string, Grant> _grants = new();

    /// <summary>
    /// Token requests received.
    /// </summary>
    public int TokenRequests { get; private set; }

    /// <summary>
    /// Approves the authorization address storeasy produced for a Google account and returns the code.
    /// </summary>
    public string Authorize(string authorizationUrl, string subject, string email, bool emailVerified = true, string givenName = "Grace", string familyName = "Hopper")
    {
        var query = HttpUtility.ParseQueryString(new Uri(authorizationUrl).Query);

        if (query["client_id"] != ClientId || query["code_challenge_method"] != "S256" || string.IsNullOrEmpty(query["code_challenge"]))
            throw new InvalidOperationException("The authorization request is not a PKCE request of the test client.");

        var code = $"code-{Guid.NewGuid():N}";

        _grants[code] = new Grant(query["code_challenge"], query["nonce"], query["redirect_uri"], subject, email, emailVerified, givenName, familyName);

        return code;
    }

    /// <inheritdoc/>
    /// <summary>
    /// A Pub/Sub push token as Google signs it: issuer <see cref="Authority"/>, the given audience and service account.
    /// </summary>
    public string PushToken(string audience, string email, bool emailVerified = true, DateTime? expires = null)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Authority,
            Audience = audience,
            IssuedAt = now.AddMinutes(-1),
            NotBefore = now.AddMinutes(-1),
            Expires = expires ?? now.AddMinutes(30),
            Claims = new Dictionary<string, object> { ["email"] = email, ["email_verified"] = emailVerified, ["sub"] = "1234567890" },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_key) { KeyId = "google-test" }, SecurityAlgorithms.RsaSha256),
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri.AbsolutePath;

        if (path == "/.well-known/openid-configuration")
        {
            return Json(new JsonObject
            {
                ["issuer"] = Authority,
                ["authorization_endpoint"] = Authority + "/o/oauth2/v2/auth",
                ["token_endpoint"] = Authority + "/token",
                ["jwks_uri"] = Authority + "/oauth2/v3/certs",
            });
        }

        if (path == "/oauth2/v3/certs")
        {
            var parameters = _key.ExportParameters(false);

            return Json(new JsonObject { ["keys"] = new JsonArray(new JsonObject { ["kty"] = "RSA", ["use"] = "sig", ["alg"] = "RS256", ["kid"] = "google-test", ["n"] = Base64UrlEncoder.Encode(parameters.Modulus), ["e"] = Base64UrlEncoder.Encode(parameters.Exponent) }) });
        }

        if (path == "/token" && request.Method == HttpMethod.Post)
        {
            TokenRequests++;

            var form = HttpUtility.ParseQueryString(await request.Content.ReadAsStringAsync(cancellationToken));

            if (form["client_id"] != ClientId || form["client_secret"] != ClientSecret || form["grant_type"] != "authorization_code" || !_grants.TryRemove(form["code"] ?? string.Empty, out var grant))
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") };

            var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"] ?? string.Empty)));

            if (challenge != grant.Challenge || form["redirect_uri"] != grant.RedirectUri)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"invalid_grant"}""") };

            return Json(new JsonObject { ["access_token"] = "ya29.test", ["token_type"] = "Bearer", ["id_token"] = IdToken(grant) });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _key.Dispose();

        base.Dispose(disposing);
    }

    private string IdToken(Grant grant)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Authority,
            Audience = ClientId,
            IssuedAt = now,
            NotBefore = now.AddSeconds(-5),
            Expires = now.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = grant.Subject,
                ["email"] = grant.Email,
                ["email_verified"] = grant.EmailVerified,
                ["given_name"] = grant.GivenName,
                ["family_name"] = grant.FamilyName,
                ["nonce"] = grant.Nonce,
            },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_key) { KeyId = "google-test" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private static HttpResponseMessage Json(JsonNode body) => new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private sealed record Grant(string Challenge, string Nonce, string RedirectUri, string Subject, string Email, bool EmailVerified, string GivenName, string FamilyName);
}
