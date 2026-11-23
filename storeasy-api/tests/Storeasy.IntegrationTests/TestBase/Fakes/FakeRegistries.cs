using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// In-memory npm, NuGet, PyPI, crates.io and OSV.dev behind the package registries reader's HTTP handler. Packages are
/// built with the helpers below in the shapes the real public APIs answer (packument with <c>dist.attestations</c> and the
/// attestations endpoint, NuGet registration index and pages, PyPI JSON and Integrity API, crates.io crate with
/// <c>trustpub_data</c>, OSV.dev batch query and advisories). An unknown address answers 404. Each test uses its own
/// package names. Registered as a singleton handler: the factory never disposes its state.
/// </summary>
public sealed class FakeRegistries : HttpMessageHandler
{
    /// <summary>npm registry address the tests configure.</summary>
    public const string NpmBaseUrl = "https://npm.registry.fake";

    /// <summary>NuGet registration address the tests configure.</summary>
    public const string NuGetBaseUrl = "https://nuget.registry.fake/v3/registration";

    /// <summary>PyPI address the tests configure.</summary>
    public const string PyPIBaseUrl = "https://pypi.registry.fake";

    /// <summary>crates.io API address the tests configure.</summary>
    public const string CratesBaseUrl = "https://crates.registry.fake/api/v1";

    /// <summary>Maven Central repository address the tests configure.</summary>
    public const string MavenRepositoryBaseUrl = "https://maven.registry.fake/maven2";

    /// <summary>Maven Central search address the tests configure.</summary>
    public const string MavenSearchBaseUrl = "https://maven-search.registry.fake/solrsearch/select";

    /// <summary>RubyGems address the tests configure.</summary>
    public const string RubyGemsBaseUrl = "https://rubygems.registry.fake";

    /// <summary>Packagist metadata address the tests configure.</summary>
    public const string PackagistBaseUrl = "https://packagist.registry.fake";

    /// <summary>pub.dev address the tests configure.</summary>
    public const string PubBaseUrl = "https://pub.registry.fake";

    /// <summary>OSV.dev API address the tests configure.</summary>
    public const string OsvBaseUrl = "https://osv.registry.fake/v1";

    /// <summary>A version of a fake package.</summary>
    /// <param name="Number">Version string.</param>
    /// <param name="Published">Publish instant.</param>
    /// <param name="Provenance">Published with provenance (npm attestation, PyPI attestation, crates.io trusted publishing).</param>
    /// <param name="Deprecated">Deprecation message (npm, NuGet).</param>
    /// <param name="Yanked">Yanked (crates.io, PyPI) or unlisted (NuGet).</param>
    public sealed record Version(string Number, DateTime Published, bool Provenance = false, string Deprecated = null, bool Yanked = false);

    /// <summary>Answers by absolute address (without query).</summary>
    public ConcurrentDictionary<string, string> Responses { get; } = new(StringComparer.Ordinal);

    /// <summary>Advisory ids by "ecosystem|name|version".</summary>
    public ConcurrentDictionary<string, List<string>> Advisories { get; } = new(StringComparer.Ordinal);

    /// <summary>Every call: "METHOD address".</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>User-Agent headers seen.</summary>
    public ConcurrentQueue<string> UserAgents { get; } = new();

    /// <summary>When set, OSV.dev answers 503.</summary>
    public bool OsvDown { get; set; }

    /// <summary>A fresh package name for a test.</summary>
    public static string NewName(string prefix = "pkg") => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    /// <summary>
    /// An npm package (unscoped or <c>@scope/name</c>) whose latest is the last version; provenance versions get an
    /// attestation naming <c>https://github.com/acme/&lt;name&gt;</c> and run <paramref name="runId"/>.
    /// </summary>
    public void SetNpm(string name, IReadOnlyList<Version> versions, long runId = 1001, string workflow = ".github/workflows/release.yml")
    {
        var encoded = name.StartsWith('@') ? "@" + name[1..].Replace("/", "%2f", StringComparison.Ordinal) : name;
        var time = new JsonObject { ["created"] = Iso(versions[0].Published), ["modified"] = Iso(versions[^1].Published) };
        var items = new JsonObject();

        foreach (var version in versions)
        {
            time[version.Number] = Iso(version.Published);

            var dist = new JsonObject { ["tarball"] = $"{NpmBaseUrl}/{encoded}/-/x-{version.Number}.tgz", ["shasum"] = "0" };

            if (version.Provenance)
            {
                dist["attestations"] = new JsonObject
                {
                    ["url"] = $"{NpmBaseUrl}/-/npm/v1/attestations/{encoded}@{version.Number}",
                    ["provenance"] = new JsonObject { ["predicateType"] = "https://slsa.dev/provenance/v1" },
                };

                Responses[$"{NpmBaseUrl}/-/npm/v1/attestations/{encoded}@{version.Number}"] = NpmAttestations(name, runId, workflow);
            }

            var item = new JsonObject { ["name"] = name, ["version"] = version.Number, ["dist"] = dist };

            if (version.Deprecated != null)
                item["deprecated"] = version.Deprecated;

            items[version.Number] = item;
        }

        Responses[$"{NpmBaseUrl}/{encoded}"] = new JsonObject
        {
            ["_id"] = name,
            ["name"] = name,
            ["dist-tags"] = new JsonObject { ["latest"] = versions[^1].Number },
            ["versions"] = items,
            ["time"] = time,
            ["repository"] = new JsonObject { ["type"] = "git", ["url"] = $"git+https://github.com/acme/{Short(name)}.git" },
        }.ToJsonString();
    }

    /// <summary>
    /// A NuGet package; with <paramref name="pagedOut"/> the page is not inlined in the index and is fetched by its address.
    /// </summary>
    public void SetNuGet(string id, IReadOnlyList<Version> versions, bool pagedOut = false)
    {
        var lower = id.ToLowerInvariant();
        var pageUrl = $"{NuGetBaseUrl}/{lower}/page/{versions[0].Number}/{versions[^1].Number}.json";
        var leaves = new JsonArray([.. versions.Select(v =>
        {
            var entry = new JsonObject
            {
                ["@id"] = $"{NuGetBaseUrl}/catalog/{lower}.{v.Number}.json",
                ["id"] = id,
                ["version"] = v.Number,
                ["listed"] = !v.Yanked,
                ["published"] = v.Yanked ? "1900-01-01T00:00:00+00:00" : Iso(v.Published),
                ["projectUrl"] = $"https://github.com/acme/{lower}",
            };

            if (v.Deprecated != null)
                entry["deprecation"] = new JsonObject { ["reasons"] = new JsonArray("Legacy"), ["message"] = v.Deprecated };

            return (JsonNode)new JsonObject { ["@id"] = $"{NuGetBaseUrl}/{lower}/{v.Number}.json", ["catalogEntry"] = entry };
        })]);

        var page = new JsonObject { ["@id"] = pageUrl, ["count"] = versions.Count, ["lower"] = versions[0].Number, ["upper"] = versions[^1].Number };

        if (pagedOut)
            Responses[pageUrl] = new JsonObject { ["@id"] = pageUrl, ["count"] = versions.Count, ["items"] = leaves }.ToJsonString();
        else
            page["items"] = leaves;

        Responses[$"{NuGetBaseUrl}/{lower}/index.json"] = new JsonObject { ["count"] = 1, ["items"] = new JsonArray(page) }.ToJsonString();
    }

    /// <summary>
    /// A PyPI project (normalized name); provenance versions answer the Integrity API with a GitHub publisher.
    /// </summary>
    public void SetPyPI(string name, IReadOnlyList<Version> versions)
    {
        var releases = new JsonObject();

        foreach (var version in versions)
        {
            var file = $"{name.Replace('-', '_')}-{version.Number}.tar.gz";

            releases[version.Number] = new JsonArray(new JsonObject
            {
                ["filename"] = file,
                ["packagetype"] = "sdist",
                ["upload_time_iso_8601"] = Iso(version.Published),
                ["yanked"] = version.Yanked,
                ["yanked_reason"] = version.Yanked ? "Broken build" : null,
            });

            var provenanceUrl = $"{PyPIBaseUrl}/integrity/{name}/{version.Number}/{file}/provenance";

            if (version.Provenance)
            {
                Responses[provenanceUrl] = new JsonObject
                {
                    ["version"] = 1,
                    ["attestation_bundles"] = new JsonArray(new JsonObject
                    {
                        ["publisher"] = new JsonObject { ["kind"] = "GitHub", ["repository"] = $"acme/{name}", ["workflow"] = "release.yml", ["environment"] = "", ["claims"] = null },
                        ["attestations"] = new JsonArray(),
                    }),
                }.ToJsonString();
            }
            else
            {
                Responses.TryRemove(provenanceUrl, out _);
            }
        }

        Responses[$"{PyPIBaseUrl}/pypi/{name}/json"] = new JsonObject
        {
            ["info"] = new JsonObject
            {
                ["name"] = name,
                ["version"] = versions.Where(v => !v.Yanked).Select(v => v.Number).LastOrDefault() ?? versions[^1].Number,
                ["project_urls"] = new JsonObject { ["Homepage"] = "https://example.com", ["Source"] = $"https://github.com/acme/{name}" },
            },
            ["releases"] = releases,
        }.ToJsonString();
    }

    /// <summary>
    /// A crate; provenance versions carry GitHub <c>trustpub_data</c> with run <paramref name="runId"/>.
    /// </summary>
    public void SetCrate(string name, IReadOnlyList<Version> versions, long runId = 2002)
    {
        var stable = versions.Where(v => !v.Yanked).Select(v => v.Number).LastOrDefault();

        Responses[$"{CratesBaseUrl}/crates/{name}"] = new JsonObject
        {
            ["crate"] = new JsonObject { ["name"] = name, ["max_version"] = stable, ["max_stable_version"] = stable, ["repository"] = $"https://github.com/acme/{name}" },
            ["versions"] = new JsonArray([.. versions.Reverse().Select(v => (JsonNode)new JsonObject
            {
                ["num"] = v.Number,
                ["created_at"] = Iso(v.Published),
                ["yanked"] = v.Yanked,
                ["yank_message"] = v.Yanked ? "Security fix" : null,
                ["trustpub_data"] = v.Provenance ? new JsonObject { ["provider"] = "github", ["repository"] = $"acme/{name}", ["run_id"] = runId.ToString(System.Globalization.CultureInfo.InvariantCulture), ["sha"] = "abc" } : null,
            })]),
        }.ToJsonString();
    }

    /// <summary>
    /// A Maven Central artifact (<c>group:artifact</c>): its maven-metadata.xml and the search answer with the dates.
    /// </summary>
    public void SetMaven(string name, IReadOnlyList<Version> versions)
    {
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        var group = name[..colon];
        var artifact = name[(colon + 1)..];
        var xml = new StringBuilder("<metadata><groupId>").Append(group).Append("</groupId><artifactId>").Append(artifact).Append("</artifactId><versioning>")
            .Append("<release>").Append(versions[^1].Number).Append("</release><versions>");

        foreach (var version in versions)
            xml.Append("<version>").Append(version.Number).Append("</version>");

        Responses[$"{MavenRepositoryBaseUrl}/{group.Replace('.', '/')}/{artifact}/maven-metadata.xml"] = xml.Append("</versions></versioning></metadata>").ToString();
        Responses[MavenSearchBaseUrl] = new JsonObject
        {
            ["response"] = new JsonObject
            {
                ["docs"] = new JsonArray([.. versions.Select(v => (JsonNode)new JsonObject { ["g"] = group, ["a"] = artifact, ["v"] = v.Number, ["timestamp"] = new DateTimeOffset(v.Published).ToUnixTimeMilliseconds() })]),
            },
        }.ToJsonString();
    }

    /// <summary>
    /// A gem; provenance versions answer the attestations endpoint with a SLSA bundle from <c>acme/&lt;name&gt;</c>.
    /// </summary>
    public void SetRubyGem(string name, IReadOnlyList<Version> versions)
    {
        Responses[$"{RubyGemsBaseUrl}/api/v1/gems/{name}.json"] = new JsonObject
        {
            ["name"] = name,
            ["version"] = versions[^1].Number,
            ["source_code_uri"] = $"https://github.com/acme/{name}",
        }.ToJsonString();
        Responses[$"{RubyGemsBaseUrl}/api/v1/versions/{name}.json"] = new JsonArray([.. versions.Reverse().Select(v => (JsonNode)new JsonObject { ["number"] = v.Number, ["created_at"] = Iso(v.Published), ["platform"] = "ruby" })]).ToJsonString();

        foreach (var version in versions)
        {
            var bundles = new JsonArray();

            if (version.Provenance)
            {
                var statement = new JsonObject
                {
                    ["predicateType"] = "https://slsa.dev/provenance/v1",
                    ["predicate"] = new JsonObject
                    {
                        ["buildDefinition"] = new JsonObject { ["externalParameters"] = new JsonObject { ["workflow"] = new JsonObject { ["repository"] = $"https://github.com/acme/{name}", ["path"] = ".github/workflows/release.yml" } } },
                        ["runDetails"] = new JsonObject { ["metadata"] = new JsonObject { ["invocationId"] = $"https://github.com/acme/{name}/actions/runs/31" } },
                    },
                };

                bundles.Add(new JsonObject { ["dsseEnvelope"] = new JsonObject { ["payload"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(statement.ToJsonString())) } });
            }

            Responses[$"{RubyGemsBaseUrl}/api/v1/attestations/{name}-{version.Number}.json"] = bundles.ToJsonString();
        }
    }

    /// <summary>
    /// A Packagist package (minified p2 metadata, newest first); <paramref name="abandoned"/> marks it abandoned for that
    /// replacement.
    /// </summary>
    public void SetPackagist(string name, IReadOnlyList<Version> versions, string abandoned = null)
    {
        var items = new JsonArray();
        var first = true;

        foreach (var version in versions.Reverse())
        {
            var item = new JsonObject { ["version"] = version.Number, ["version_normalized"] = version.Number + ".0", ["time"] = Iso(version.Published) };

            if (first)
            {
                item["name"] = name;
                item["source"] = new JsonObject { ["url"] = $"https://github.com/{name}.git", ["type"] = "git" };

                if (abandoned != null)
                    item["abandoned"] = abandoned;

                first = false;
            }

            items.Add(item);
        }

        Responses[$"{PackagistBaseUrl}/p2/{name}.json"] = new JsonObject { ["minified"] = "composer/2.0", ["packages"] = new JsonObject { [name] = items } }.ToJsonString();
    }

    /// <summary>
    /// A pub.dev package; <paramref name="replacedBy"/> marks it discontinued in favour of that package.
    /// </summary>
    public void SetPub(string name, IReadOnlyList<Version> versions, string replacedBy = null)
    {
        Responses[$"{PubBaseUrl}/api/packages/{name}"] = new JsonObject
        {
            ["name"] = name,
            ["latest"] = new JsonObject { ["version"] = versions[^1].Number, ["pubspec"] = new JsonObject { ["repository"] = $"https://github.com/acme/{name}" }, ["published"] = Iso(versions[^1].Published) },
            ["versions"] = new JsonArray([.. versions.Select(v => (JsonNode)new JsonObject { ["version"] = v.Number, ["published"] = Iso(v.Published), ["retracted"] = v.Yanked })]),
        }.ToJsonString();
        Responses[$"{PubBaseUrl}/api/packages/{name}/options"] = new JsonObject { ["isDiscontinued"] = replacedBy != null, ["replacedBy"] = replacedBy, ["isUnlisted"] = false }.ToJsonString();
    }

    /// <summary>
    /// An OSV.dev advisory for one package version.
    /// </summary>
    public void SetAdvisory(string ecosystem, string name, string version, string id, string severity = "HIGH", string summary = "Remote code execution")
    {
        Advisories.AddOrUpdate($"{ecosystem}|{name}|{version}", [id], (_, list) => [.. list.Append(id).Distinct()]);
        Responses[$"{OsvBaseUrl}/vulns/{id}"] = new JsonObject
        {
            ["id"] = id,
            ["summary"] = summary,
            ["database_specific"] = new JsonObject { ["severity"] = severity },
            ["affected"] = new JsonArray(new JsonObject { ["package"] = new JsonObject { ["ecosystem"] = ecosystem, ["name"] = name } }),
        }.ToJsonString();
    }

    /// <summary>
    /// Removes every advisory of one package version (fixed).
    /// </summary>
    public void ClearAdvisories(string ecosystem, string name, string version) => Advisories.TryRemove($"{ecosystem}|{name}|{version}", out _);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.GetLeftPart(UriPartial.Path).Replace("%2F", "%2f", StringComparison.Ordinal);

        Calls.Enqueue($"{request.Method} {url}");
        UserAgents.Enqueue(request.Headers.UserAgent.ToString());

        if (url.StartsWith(OsvBaseUrl, StringComparison.Ordinal) && OsvDown)
            return Json(HttpStatusCode.ServiceUnavailable, "{}");

        if (request.Method == HttpMethod.Post && url == $"{OsvBaseUrl}/querybatch")
            return Json(HttpStatusCode.OK, QueryBatch(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))));

        return Responses.TryGetValue(url, out var body) ? Json(HttpStatusCode.OK, body) : Json(HttpStatusCode.NotFound, "{\"error\":\"Not found\"}");
    }

    private string QueryBatch(JsonNode body)
    {
        var results = new JsonArray();

        foreach (var query in body?["queries"] as JsonArray ?? [])
        {
            var key = $"{query?["package"]?["ecosystem"]}|{query?["package"]?["name"]}|{query?["version"]}";
            var ids = Advisories.TryGetValue(key, out var list) ? list : [];

            results.Add(ids.Count == 0 ? new JsonObject() : new JsonObject { ["vulns"] = new JsonArray([.. ids.Select(id => (JsonNode)new JsonObject { ["id"] = id, ["modified"] = "2026-11-01T00:00:00Z" })]) });
        }

        return new JsonObject { ["results"] = results }.ToJsonString();
    }

    private static string NpmAttestations(string name, long runId, string workflow)
    {
        var statement = new JsonObject
        {
            ["_type"] = "https://in-toto.io/Statement/v1",
            ["predicateType"] = "https://slsa.dev/provenance/v1",
            ["predicate"] = new JsonObject
            {
                ["buildDefinition"] = new JsonObject
                {
                    ["externalParameters"] = new JsonObject
                    {
                        ["workflow"] = new JsonObject { ["ref"] = "refs/heads/main", ["repository"] = $"https://github.com/acme/{Short(name)}", ["path"] = workflow },
                    },
                },
                ["runDetails"] = new JsonObject
                {
                    ["builder"] = new JsonObject { ["id"] = "https://github.com/actions/runner/github-hosted" },
                    ["metadata"] = new JsonObject { ["invocationId"] = $"https://github.com/acme/{Short(name)}/actions/runs/{runId}/attempts/1" },
                },
            },
        };

        return new JsonObject
        {
            ["attestations"] = new JsonArray(
                new JsonObject { ["predicateType"] = "https://github.com/npm/attestation/tree/main/specs/publish/v0.1", ["bundle"] = new JsonObject() },
                new JsonObject
                {
                    ["predicateType"] = "https://slsa.dev/provenance/v1",
                    ["bundle"] = new JsonObject { ["dsseEnvelope"] = new JsonObject { ["payload"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(statement.ToJsonString())), ["payloadType"] = "application/vnd.in-toto+json" } },
                }),
        }.ToJsonString();
    }

    private static string Short(string name) => name.StartsWith('@') ? name[(name.IndexOf('/') + 1)..] : name;

    private static string Iso(DateTime date) => date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
