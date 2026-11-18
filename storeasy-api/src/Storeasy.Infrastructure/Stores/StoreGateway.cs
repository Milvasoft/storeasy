using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores;

/// <summary>
/// Credential inspection and verification against App Store Connect and Google Play. Uses the named HTTP client
/// <see cref="HttpClientName"/>; tests replace its handler.
/// </summary>
/// <param name="HttpClientFactory"></param>
/// <param name="TimeProvider"></param>
/// <param name="Logger"></param>
public sealed partial class StoreGateway(IHttpClientFactory HttpClientFactory, TimeProvider TimeProvider, IMilvaLogger Logger) : IStoreGateway
{
    /// <summary>
    /// Name of the HTTP client used for store calls.
    /// </summary>
    public const string HttpClientName = "stores";

    /// <summary>
    /// App Store Connect API base address.
    /// </summary>
    public const string AppleBaseUrl = "https://api.appstoreconnect.apple.com";

    /// <summary>
    /// Google Play Developer API base address.
    /// </summary>
    public const string GoogleBaseUrl = "https://androidpublisher.googleapis.com/androidpublisher/v3";

    /// <summary>
    /// OAuth scope of the Google Play Developer API.
    /// </summary>
    public const string GoogleScope = "https://www.googleapis.com/auth/androidpublisher";

    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = HttpClientFactory;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IMilvaLogger _logger = Logger;

    /// <inheritdoc/>
    public StoreSecretInspection Inspect(StoreKind store, string secretJson)
    {
        if (store is not (StoreKind.Apple or StoreKind.Google))
        {
            return new StoreSecretInspection(false, MessageKey.StoreNotSupported, null, null);
        }

        if (store == StoreKind.Apple)
        {
            var apple = StoreCredentialParser.ParseApple(secretJson, out var appleError);

            if (apple == null)
            {
                return new StoreSecretInspection(false, appleError, null, null);
            }

            var fingerprint = new ConnectionFingerprint
            {
                KeyId = apple.KeyId,
                IssuerIdSuffix = apple.IssuerId[^4..],
            };

            return new StoreSecretInspection(true, null, fingerprint, JsonSerializer.Serialize(apple));
        }

        var google = StoreCredentialParser.ParseGoogle(secretJson, out var googleError);

        if (google == null)
        {
            return new StoreSecretInspection(false, googleError, null, null);
        }

        var googleFingerprint = new ConnectionFingerprint
        {
            ServiceAccountEmail = google.ClientEmail,
            ProjectId = google.ProjectId,
        };

        return new StoreSecretInspection(true, null, googleFingerprint, secretJson);
    }

    /// <inheritdoc/>
    public async Task<StoreVerification> VerifyAsync(StoreKind store, string secretJson, string packageName, CancellationToken cancellationToken)
    {
        try
        {
            return store switch
            {
                StoreKind.Apple => await VerifyAppleAsync(secretJson, cancellationToken),
                StoreKind.Google => await VerifyGoogleAsync(secretJson, packageName, cancellationToken),
                _ => new StoreVerification(ConnectionStatus.Rejected, NotSupportedMessage(store)),
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.Warning("Store verification could not reach {Store}: {Error}", store, SecretRedactor.Redact(ex.Message));

            return new StoreVerification(ConnectionStatus.Unreachable, SecretRedactor.RedactAndTrim(ex.Message, _maxErrorLength));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new StoreVerification(ConnectionStatus.Unreachable, "Timed out.");
        }
    }

    private async Task<StoreVerification> VerifyAppleAsync(string secretJson, CancellationToken cancellationToken)
    {
        var credentials = StoreCredentialParser.ParseApple(secretJson, out var errorKey);

        if (credentials == null)
        {
            return new StoreVerification(ConnectionStatus.Rejected, errorKey);
        }

        var token = StoreJwt.CreateAppleToken(credentials, _timeProvider.GetUtcNow());

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{AppleBaseUrl}/v1/apps?limit=1&fields[apps]=bundleId");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

        return await ToVerificationAsync(response, cancellationToken);
    }

    private async Task<StoreVerification> VerifyGoogleAsync(string secretJson, string packageName, CancellationToken cancellationToken)
    {
        var credentials = StoreCredentialParser.ParseGoogle(secretJson, out var errorKey);

        if (credentials == null)
        {
            return new StoreVerification(ConnectionStatus.Rejected, errorKey);
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var assertion = StoreJwt.CreateGoogleAssertion(credentials, GoogleScope, _timeProvider.GetUtcNow());

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, credentials.TokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion,
            }),
        };

        using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return await ToVerificationAsync(tokenResponse, cancellationToken);
        }

        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));

        if (!tokenDocument.RootElement.TryGetProperty("access_token", out var accessToken))
        {
            return new StoreVerification(ConnectionStatus.Rejected, "Token response has no access token.");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            return new StoreVerification(ConnectionStatus.Verified, null);
        }

        // An edit is the cheapest call that proves the service account was invited to the app; it is deleted right away.
        using var editRequest = new HttpRequestMessage(HttpMethod.Post, $"{GoogleBaseUrl}/applications/{Uri.EscapeDataString(packageName)}/edits");
        editRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.GetString());
        editRequest.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        using var editResponse = await client.SendAsync(editRequest, cancellationToken);

        if (!editResponse.IsSuccessStatusCode)
        {
            return await ToVerificationAsync(editResponse, cancellationToken);
        }

        using var editDocument = JsonDocument.Parse(await editResponse.Content.ReadAsStringAsync(cancellationToken));

        if (editDocument.RootElement.TryGetProperty("id", out var editId))
        {
            using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"{GoogleBaseUrl}/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId.GetString() ?? string.Empty)}");
            deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.GetString());

            using var deleteResponse = await client.SendAsync(deleteRequest, cancellationToken);
        }

        return new StoreVerification(ConnectionStatus.Verified, null);
    }

    /// <summary>
    /// Error text for a store the engine does not talk to yet (Microsoft Store until phase 5).
    /// </summary>
    internal static string NotSupportedMessage(StoreKind store) => $"{store} is not supported yet.";

    private static async Task<StoreVerification> ToVerificationAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return new StoreVerification(ConnectionStatus.Verified, null);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = SecretRedactor.RedactAndTrim($"{(int)response.StatusCode}: {DescribeError(body)}", _maxErrorLength);
        var status = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest or HttpStatusCode.NotFound
            ? ConnectionStatus.Rejected
            : ConnectionStatus.Unreachable;

        return new StoreVerification(status, error);
    }

    /// <summary>
    /// Extracts the human readable part of an Apple (<c>errors[].detail</c>) or Google (<c>error.message</c>,
    /// <c>error_description</c>) error body; falls back to the raw text.
    /// </summary>
    internal static string DescribeError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "empty response";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                return string.Join("; ", errors.EnumerateArray().Select(e => e.TryGetProperty("detail", out var d) ? d.GetString() : e.TryGetProperty("title", out var t) ? t.GetString() : null).Where(m => m != null));
            }

            if (root.TryGetProperty("error_description", out var description))
            {
                return description.GetString();
            }

            if (root.TryGetProperty("error", out var error))
            {
                return error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) ? message.GetString() : error.ToString();
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }
}
