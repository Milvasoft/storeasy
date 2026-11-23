using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Enums;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="IRegistryWriter"/>: the registries' documented write calls, with the organization's stored key, only from an
/// approved plan.
/// <list type="bullet">
/// <item>NuGet: <c>DELETE</c> (unlist) and <c>POST</c> (relist) <c>{publish}/{id}/{version}</c> with <c>X-NuGet-ApiKey</c>.</item>
/// <item>npm: deprecation is the packument read with <c>?write=true</c> and put back with <c>versions[v].deprecated</c> set
/// (as the npm CLI does); a dist-tag is <c>PUT /-/package/{name}/dist-tags/{tag}</c> with the version as a JSON string;
/// <c>Authorization: Bearer</c>.</item>
/// <item>crates.io: <c>DELETE /crates/{name}/{version}/yank</c> and <c>PUT .../unyank</c> with the token as
/// <c>Authorization</c>.</item>
/// <item>RubyGems: <c>DELETE /api/v1/gems/yank</c> with <c>gem_name</c> and <c>version</c> and the key as
/// <c>Authorization</c>.</item>
/// </list>
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed class RegistryWriter(IHttpClientFactory httpClientFactory, RegistryOptions options) : IRegistryWriter
{
    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly RegistryOptions _options = options;

    /// <inheritdoc/>
    public async Task WriteAsync(RegistryPlanKind kind, PackageRegistry registry, string name, string version, string argument, string secret, CancellationToken cancellationToken)
    {
        if (!RegistrySecrets.Supports(registry, kind))
            throw new StoreCallException($"{RegistryNames.DisplayName(registry)} has no API for {kind}.", null, transient: false);

        var path = RegistryNames.PathName(registry, name);
        var escapedVersion = Uri.EscapeDataString(version);

        switch (registry)
        {
            case PackageRegistry.NuGet:
                await SendAsync(kind == RegistryPlanKind.Unlist ? HttpMethod.Delete : HttpMethod.Post, $"{_options.NuGetPublishUrl.TrimEnd('/')}/{Uri.EscapeDataString(name)}/{escapedVersion}", null, r => r.Headers.Add("X-NuGet-ApiKey", secret), cancellationToken);
                break;
            case PackageRegistry.Npm when kind == RegistryPlanKind.DistTag:
                await SendAsync(HttpMethod.Put, $"{Base(_options.NpmBaseUrl)}/-/package/{path}/dist-tags/{Uri.EscapeDataString(argument)}", Json(JsonSerializer.Serialize(version)), Bearer(secret), cancellationToken);
                break;
            case PackageRegistry.Npm:
                await DeprecateAsync(path, version, kind == RegistryPlanKind.Deprecate ? argument : string.Empty, secret, cancellationToken);
                break;
            case PackageRegistry.Crates:
                await SendAsync(kind == RegistryPlanKind.Yank ? HttpMethod.Delete : HttpMethod.Put, $"{Base(_options.CratesBaseUrl)}/crates/{path}/{escapedVersion}/{(kind == RegistryPlanKind.Yank ? "yank" : "unyank")}", null, Raw(secret), cancellationToken);
                break;
            default:
                var form = new FormUrlEncodedContent([KeyValuePair.Create("gem_name", name), KeyValuePair.Create("version", version)]);
                await SendAsync(HttpMethod.Delete, $"{Base(_options.RubyGemsBaseUrl)}/api/v1/gems/yank", form, Raw(secret), cancellationToken);
                break;
        }
    }

    private async Task DeprecateAsync(string path, string version, string message, string secret, CancellationToken cancellationToken)
    {
        var url = $"{Base(_options.NpmBaseUrl)}/{path}";
        var packument = JsonNode.Parse(await SendAsync(HttpMethod.Get, $"{url}?write=true", null, Bearer(secret), cancellationToken) ?? "{}") as JsonObject;

        if (packument?["versions"]?[version] is not JsonObject entry)
            throw new StoreCallException($"npm has no version {version}.", 404, transient: false);

        entry["deprecated"] = message;

        await SendAsync(HttpMethod.Put, url, Json(packument.ToJsonString()), Bearer(secret), cancellationToken);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static Action<HttpRequestMessage> Bearer(string secret) => r => r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);

    private static Action<HttpRequestMessage> Raw(string secret) => r => r.Headers.TryAddWithoutValidation("Authorization", secret);

    private async Task<string> SendAsync(HttpMethod method, string url, HttpContent content, Action<HttpRequestMessage> authorize, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        authorize(request);
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        try
        {
            using var response = await _httpClientFactory.CreateClient(PackageRegistryReader.HttpClientName).SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return text;

            var status = (int)response.StatusCode;

            throw new StoreCallException($"{new Uri(url).Host} answered {status}: {Shorten(text)}", status, status == 429 || status >= 500);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException($"{new Uri(url).Host} could not be reached: {Shorten(ex.Message)}", null, transient: true);
        }
    }

    private static string Base(string url) => url.TrimEnd('/');

    private static string Shorten(string text) => string.IsNullOrEmpty(text) || text.Length <= _maxErrorLength ? text : text[.._maxErrorLength];
}
