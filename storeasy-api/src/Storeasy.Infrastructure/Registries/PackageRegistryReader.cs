using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Enums;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="IPackageRegistryReader"/> over the public APIs of npm, NuGet, PyPI, crates.io and OSV.dev (named HTTP client
/// <see cref="HttpClientName"/>, anonymous, with <see cref="RegistryOptions.UserAgent"/>). A 404 is "not found"; 429 and 5xx
/// answers, timeouts and network errors are transient <see cref="StoreCallException"/>s. Tests replace the handler with fake
/// registries.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed class PackageRegistryReader(IHttpClientFactory httpClientFactory, RegistryOptions options) : IPackageRegistryReader
{
    /// <summary>
    /// Name of the HTTP client.
    /// </summary>
    public const string HttpClientName = "registries";

    /// <summary>
    /// Media type of the PyPI Integrity API.
    /// </summary>
    public const string PyPIIntegrityMediaType = "application/vnd.pypi.integrity.v1+json";

    private const int _maxNuGetPages = 50;
    private const int _maxErrorLength = 300;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly RegistryOptions _options = options;

    /// <inheritdoc/>
    public async Task<RegistryPackageRead> ReadPackageAsync(PackageRegistry registry, string name, CancellationToken cancellationToken)
    {
        var path = RegistryNames.PathName(registry, name);

        switch (registry)
        {
            case PackageRegistry.Npm:
                var npm = await GetAsync($"{Base(_options.NpmBaseUrl)}/{path}", null, cancellationToken);
                return npm == null ? null : RegistryParsers.ParseNpm(npm);
            case PackageRegistry.NuGet:
                return await ReadNuGetAsync(path, cancellationToken);
            case PackageRegistry.PyPI:
                var pypi = await GetAsync($"{Base(_options.PyPIBaseUrl)}/pypi/{path}/json", null, cancellationToken);
                return pypi == null ? null : RegistryParsers.ParsePyPI(pypi);
            case PackageRegistry.Crates:
                var crate = await GetAsync($"{Base(_options.CratesBaseUrl)}/crates/{path}", null, cancellationToken);
                return crate == null ? null : RegistryParsers.ParseCrate(crate);
            case PackageRegistry.Maven:
                return await ReadMavenAsync(name, cancellationToken);
            case PackageRegistry.RubyGems:
                var gem = await GetAsync($"{Base(_options.RubyGemsBaseUrl)}/api/v1/gems/{path}.json", null, cancellationToken);
                return gem == null ? null : RegistryParsers.ParseRubyGems(gem, await GetAsync($"{Base(_options.RubyGemsBaseUrl)}/api/v1/versions/{path}.json", null, cancellationToken));
            case PackageRegistry.Packagist:
                var lower = name.ToLowerInvariant();
                var p2 = await GetAsync($"{Base(_options.PackagistBaseUrl)}/p2/{lower}.json", null, cancellationToken);
                return p2 == null ? null : RegistryParsers.ParsePackagist(lower, p2);
            case PackageRegistry.Pub:
                var pub = await GetAsync($"{Base(_options.PubBaseUrl)}/api/packages/{path}", null, cancellationToken);
                return pub == null ? null : RegistryParsers.ParsePub(pub, await TryGetAsync($"{Base(_options.PubBaseUrl)}/api/packages/{path}/options", cancellationToken));
            default:
                throw new StoreCallException($"Unknown registry {registry}.", null, transient: false);
        }
    }

    /// <summary>
    /// Maven Central: the versions from <c>maven-metadata.xml</c>, their dates from the search API (a failed search only
    /// leaves the dates empty).
    /// </summary>
    private async Task<RegistryPackageRead> ReadMavenAsync(string name, CancellationToken cancellationToken)
    {
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        var group = name[..colon];
        var artifact = name[(colon + 1)..];
        var metadata = await GetAsync($"{Base(_options.MavenRepositoryBaseUrl)}/{group.Replace('.', '/')}/{Uri.EscapeDataString(artifact)}/maven-metadata.xml", "application/xml", cancellationToken);

        if (metadata == null)
            return null;

        var search = await TryGetAsync($"{Base(_options.MavenSearchBaseUrl)}?q=g:{Uri.EscapeDataString(group)}+AND+a:{Uri.EscapeDataString(artifact)}&core=gav&rows=200&wt=json", cancellationToken);

        return RegistryParsers.ParseMaven(name, metadata, search);
    }

    /// <summary>
    /// A read whose failure only leaves an optional value out.
    /// </summary>
    private async Task<string> TryGetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await GetAsync(url, null, cancellationToken);
        }
        catch (StoreCallException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ProvenanceRead> ReadProvenanceAsync(PackageRegistry registry, string name, RegistryVersionRead version, CancellationToken cancellationToken)
    {
        switch (registry)
        {
            case PackageRegistry.Npm:
                var npm = await GetAsync($"{Base(_options.NpmBaseUrl)}/-/npm/v1/attestations/{RegistryNames.PathName(registry, name)}@{Uri.EscapeDataString(version.Version)}", null, cancellationToken);
                return npm == null ? null : RegistryParsers.ParseNpmProvenance(npm);
            case PackageRegistry.PyPI when !string.IsNullOrEmpty(version.ArtifactFile):
                var url = $"{Base(_options.PyPIBaseUrl)}/integrity/{RegistryNames.PathName(registry, name)}/{Uri.EscapeDataString(version.Version)}/{Uri.EscapeDataString(version.ArtifactFile)}/provenance";
                var pypi = await GetAsync(url, PyPIIntegrityMediaType, cancellationToken);
                return pypi == null ? null : RegistryParsers.ParsePyPIProvenance(pypi);
            case PackageRegistry.RubyGems:
                var gem = await GetAsync($"{Base(_options.RubyGemsBaseUrl)}/api/v1/attestations/{RegistryNames.PathName(registry, name)}-{Uri.EscapeDataString(version.Version)}.json", null, cancellationToken);
                return gem == null ? null : RegistryParsers.ParseRubyGemsAttestations(gem);
            case PackageRegistry.Crates when version.HasProvenance == true:
                return new ProvenanceRead(version.SourceRepository, null, version.CiRunUrl);
            default:
                return null;
        }
    }

    /// <inheritdoc/>
    public async Task<List<List<RegistryAdvisory>>> ReadAdvisoriesAsync(IReadOnlyList<AdvisoryQuery> queries, CancellationToken cancellationToken)
    {
        if (queries.Count == 0)
            return [];

        var body = new JsonObject
        {
            ["queries"] = new JsonArray([.. queries.Select(q => (JsonNode)new JsonObject
            {
                ["package"] = new JsonObject { ["name"] = q.Name, ["ecosystem"] = RegistryNames.OsvEcosystem(q.Registry) },
                ["version"] = q.Version,
            })]),
        };

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        var batch = RegistryParsers.ParseOsvBatch(await SendAsync(HttpMethod.Post, $"{Base(_options.OsvBaseUrl)}/querybatch", content, null, cancellationToken) ?? string.Empty);
        var details = new Dictionary<string, RegistryAdvisory>(StringComparer.Ordinal);

        foreach (var id in batch.SelectMany(ids => ids).Distinct(StringComparer.Ordinal).Take(Math.Max(0, _options.MaxAdvisoryDetails)))
        {
            var vulnerability = await GetAsync($"{Base(_options.OsvBaseUrl)}/vulns/{Uri.EscapeDataString(id)}", null, cancellationToken);
            var advisory = vulnerability == null ? null : RegistryParsers.ParseOsvVulnerability(vulnerability);

            details[id] = advisory;
        }

        return [.. queries.Select((_, index) => index < batch.Count
            ? batch[index].Where(id => !details.TryGetValue(id, out var detail) || detail != null)
                          .Select(id => details.GetValueOrDefault(id) ?? new RegistryAdvisory(id, null, null, RegistryNames.AdvisoryUrl(id)))
                          .ToList()
            : [])];
    }

    private async Task<RegistryPackageRead> ReadNuGetAsync(string path, CancellationToken cancellationToken)
    {
        var index = await GetAsync($"{Base(_options.NuGetRegistrationBaseUrl)}/{path}/index.json", null, cancellationToken);

        if (index == null)
            return null;

        var leaves = new List<JsonNode>();

        foreach (var (pageUrl, inline) in RegistryParsers.ParseNuGetIndex(index).Take(_maxNuGetPages))
        {
            var items = inline;

            if (items == null && !string.IsNullOrEmpty(pageUrl))
                items = RegistryParsers.ParseNuGetPage(await GetAsync(pageUrl, null, cancellationToken));

            leaves.AddRange(items ?? []);
        }

        return RegistryParsers.ParseNuGetLeaves(leaves);
    }

    private Task<string> GetAsync(string url, string accept, CancellationToken cancellationToken) => SendAsync(HttpMethod.Get, url, null, accept, cancellationToken);

    private async Task<string> SendAsync(HttpMethod method, string url, HttpContent content, string accept, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept ?? "application/json"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var response = await client.SendAsync(request, timeout.Token);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (response.IsSuccessStatusCode)
                return body;

            var status = (int)response.StatusCode;
            var transient = status == 429 || status >= 500;

            throw new StoreCallException($"{new Uri(url).Host} answered {status}: {Shorten(body)}", status, transient);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new StoreCallException($"{new Uri(url).Host} did not answer in time.", null, transient: true);
        }
        catch (HttpRequestException ex)
        {
            throw new StoreCallException($"{new Uri(url).Host} could not be reached: {Shorten(ex.Message)}", null, transient: true);
        }
    }

    private static string Base(string url) => url.TrimEnd('/');

    private static string Shorten(string text) => string.IsNullOrEmpty(text) || text.Length <= _maxErrorLength ? text : text[.._maxErrorLength];
}
