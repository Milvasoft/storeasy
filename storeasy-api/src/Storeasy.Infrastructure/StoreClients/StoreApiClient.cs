using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Infrastructure.Stores;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreClients;

/// <summary>
/// Small JSON client of the events phase gateways over the <see cref="StoreGateway.HttpClientName"/> client: bearer
/// tokens from the stored secrets, JSON bodies, App Store Connect pagination and errors as <see cref="StoreCallException"/>
/// (429 and 5xx transient). Every returned element is a clone, safe after the document is gone.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="timeProvider"></param>
public sealed class StoreApiClient(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
{
    /// <summary>
    /// Most pages read from one App Store Connect list.
    /// </summary>
    public const int MaxPages = 5;

    private const int _errorMaxLength = 300;
    private static readonly JsonSerializerOptions _json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// A short lived App Store Connect token for the stored secret.
    /// </summary>
    public string AppleToken(string secretJson)
    {
        var credentials = StoreCredentialParser.ParseApple(secretJson, out var errorKey);

        return credentials == null ? throw new StoreCallException(errorKey, null, false) : StoreJwt.CreateAppleToken(credentials, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// A Google access token for the stored service account.
    /// </summary>
    public async Task<string> GoogleTokenAsync(string secretJson, CancellationToken cancellationToken)
    {
        var credentials = StoreCredentialParser.ParseGoogle(secretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);
        var assertion = StoreJwt.CreateGoogleAssertion(credentials, StoreGateway.GoogleScope, _timeProvider.GetUtcNow());

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion,
        });

        var root = await SendAsync(HttpMethod.Post, credentials.TokenUri, null, form, cancellationToken);

        return Text(root, "access_token") ?? throw new StoreCallException("Token response has no access token.", null, false);
    }

    /// <summary>
    /// Sends a request with an optional JSON body (an object is serialized) and returns the parsed answer (<c>{}</c> when empty).
    /// </summary>
    public Task<JsonElement> JsonAsync(HttpMethod method, string url, string token, object body, CancellationToken cancellationToken)
        => SendAsync(method, url, token, body == null ? null : new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json"), cancellationToken);

    /// <summary>
    /// Like <see cref="JsonAsync"/>, but a 404 answer returns null instead of throwing.
    /// </summary>
    public async Task<JsonElement?> JsonOrNotFoundAsync(HttpMethod method, string url, string token, object body, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonAsync(method, url, token, body, cancellationToken);
        }
        catch (StoreCallException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an App Store Connect list following its <c>links.next</c>, at most <paramref name="maxPages"/> pages.
    /// Returns the <c>data</c> items and the <c>included</c> items of every page.
    /// </summary>
    public async Task<(List<JsonElement> Data, List<JsonElement> Included)> AppleListAsync(string url, string token, CancellationToken cancellationToken, int maxPages = MaxPages)
    {
        var data = new List<JsonElement>();
        var included = new List<JsonElement>();

        for (var page = 0; url != null && page < maxPages; page++)
        {
            var root = await JsonAsync(HttpMethod.Get, url, token, null, cancellationToken);

            data.AddRange(Items(root, "data"));
            included.AddRange(Items(root, "included"));

            url = root.TryGetProperty("links", out var links) ? Text(links, "next") : null;
        }

        return (data, included);
    }

    /// <summary>
    /// The array items of <paramref name="name"/>, or none.
    /// </summary>
    public static IEnumerable<JsonElement> Items(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    /// <summary>
    /// A string property, or null.
    /// </summary>
    public static string Text(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// A boolean property, or false.
    /// </summary>
    public static bool Flag(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>
    /// An integer property, or null.
    /// </summary>
    public static int? Number(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    /// <summary>
    /// An ISO date property as UTC, or null.
    /// </summary>
    public static DateTime? Date(JsonElement element, string name)
        => DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.UtcDateTime : null;

    /// <summary>
    /// A nested object, or an undefined element.
    /// </summary>
    public static JsonElement Child(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    /// <summary>
    /// The id of a to-one relationship (<c>relationships.{name}.data.id</c>), or null.
    /// </summary>
    public static string RelationshipId(JsonElement resource, string name)
    {
        var data = Child(Child(Child(resource, "relationships"), name), "data");

        return data.ValueKind == JsonValueKind.Object ? Text(data, "id") : null;
    }

    /// <summary>
    /// The ids of a to-many relationship, or none.
    /// </summary>
    public static List<string> RelationshipIds(JsonElement resource, string name)
        => [.. Items(Child(Child(resource, "relationships"), name), "data").Select(d => Text(d, "id")).Where(id => id != null)];

    private async Task<JsonElement> SendAsync(HttpMethod method, string url, string token, HttpContent content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;

        try
        {
            response = await _httpClientFactory.CreateClient(StoreGateway.HttpClientName).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException(SecretRedactor.RedactAndTrim($"Store unreachable: {ex.Message}", _errorMaxLength), null, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException("The store did not answer in time.", null, true);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
                throw new StoreCallException(SecretRedactor.RedactAndTrim($"{status}: {StoreGateway.DescribeError(text)}", _errorMaxLength), status, status == 429 || status >= 500);

            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);

            return document.RootElement.Clone();
        }
    }
}
