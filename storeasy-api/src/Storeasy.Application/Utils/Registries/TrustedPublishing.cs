using Storeasy.Domain.Registries;
using System.Text;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// One value the member fills in on the registry's trusted publisher settings page.
/// </summary>
/// <param name="Key">Field: <c>owner</c>, <c>repository</c>, <c>workflow</c>, <c>environment</c>, <c>namespace</c>,
/// <c>project</c>, <c>pipelineFile</c>, <c>repositoryPath</c> or <c>tagPattern</c>.</param>
/// <param name="Value">What to fill in.</param>
public sealed record SetupField(string Key, string Value);

/// <summary>
/// Result of <see cref="TrustedPublishing.Check"/>.
/// </summary>
/// <param name="Status">Outcome.</param>
/// <param name="ExpectedRepository">Repository address the setup expects.</param>
/// <param name="ExpectedWorkflow">Workflow file the setup expects.</param>
/// <param name="ObservedRepository">Repository the latest version's provenance names.</param>
/// <param name="ObservedWorkflow">Workflow the latest version's provenance names.</param>
/// <param name="Version">The latest version.</param>
public sealed record PublisherCheck(PublisherCheckStatus Status, string ExpectedRepository, string ExpectedWorkflow, string ObservedRepository, string ObservedWorkflow, string Version);

/// <summary>
/// The trusted publishing wizard (docs/package-registries.md): which registries offer trusted publishing from which CI,
/// what to fill in on the registry's settings page, a ready to copy CI file that publishes with OIDC (and provenance where
/// the registry records it), and the check of the latest release against the setup. storeasy never writes to a registry
/// or a repository: the CI file is text for the member to commit.
/// </summary>
public static partial class TrustedPublishing
{
    [GeneratedRegex(@"^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$")]
    private static partial Regex GitHubRepository();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)+$")]
    private static partial Regex GitLabRepository();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+\.ya?ml$")]
    private static partial Regex WorkflowFile();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*\.ya?ml$")]
    private static partial Regex PipelineFile();

    [GeneratedRegex(@"^[A-Za-z0-9._-][A-Za-z0-9._ -]*$")]
    private static partial Regex EnvironmentName();

    /// <summary>
    /// CI providers the registry accepts trusted publishing from (empty: the registry has none). npm and PyPI take GitHub
    /// Actions and GitLab CI/CD; crates.io, NuGet (storeasy writes its GitHub login action only), RubyGems and pub.dev take
    /// GitHub Actions. Maven Central and Packagist have no trusted publishing.
    /// </summary>
    public static IReadOnlyList<CiProvider> Providers(PackageRegistry registry) => registry switch
    {
        PackageRegistry.Npm or PackageRegistry.PyPI => [CiProvider.GitHubActions, CiProvider.GitLabCi],
        PackageRegistry.Crates or PackageRegistry.NuGet or PackageRegistry.RubyGems or PackageRegistry.Pub => [CiProvider.GitHubActions],
        _ => [],
    };

    /// <summary>
    /// Where the trusted publisher is set up on the registry.
    /// </summary>
    public static string SettingsUrl(PackageRegistry registry, string name) => registry switch
    {
        PackageRegistry.Npm => $"https://www.npmjs.com/package/{name}/access",
        PackageRegistry.PyPI => $"https://pypi.org/manage/project/{RegistryNames.NormalizePyPI(name)}/settings/publishing/",
        PackageRegistry.Crates => $"https://crates.io/crates/{name}/settings",
        PackageRegistry.NuGet => "https://www.nuget.org/account/trustedpublishing",
        PackageRegistry.RubyGems => $"https://rubygems.org/gems/{name}/trusted_publishers",
        PackageRegistry.Pub => $"https://pub.dev/packages/{name}/admin",
        _ => null,
    };

    /// <summary>
    /// Whether <paramref name="value"/> is a repository of <paramref name="provider"/>: <c>owner/repo</c> on GitHub,
    /// <c>namespace/project</c> (with subgroups) on GitLab.
    /// </summary>
    public static bool IsRepository(CiProvider provider, string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= TrackedPackage.SetupRepositoryMaxLength
           && (provider == CiProvider.GitLabCi ? GitLabRepository().IsMatch(value) : GitHubRepository().IsMatch(value));

    /// <summary>
    /// Whether <paramref name="value"/> is a workflow of <paramref name="provider"/>: a file name in
    /// <c>.github/workflows</c> on GitHub, the top-level CI file path on GitLab.
    /// </summary>
    public static bool IsWorkflow(CiProvider provider, string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= TrackedPackage.SetupWorkflowMaxLength && !value.Contains("..", StringComparison.Ordinal)
           && (provider == CiProvider.GitLabCi ? PipelineFile().IsMatch(value) : WorkflowFile().IsMatch(value));

    /// <summary>
    /// Whether <paramref name="value"/> is an environment (or registry user) name.
    /// </summary>
    public static bool IsName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= TrackedPackage.SetupEnvironmentMaxLength && EnvironmentName().IsMatch(value);

    /// <summary>
    /// The path the CI file is saved at in the repository.
    /// </summary>
    public static string WorkflowPath(CiProvider provider, string workflow) => provider == CiProvider.GitLabCi ? workflow : $".github/workflows/{workflow}";

    /// <summary>
    /// What to fill in on the registry's trusted publisher settings, in the order the registry asks for it.
    /// </summary>
    public static List<SetupField> RegistryFields(PackageRegistry registry, CiProvider provider, string repository, string workflow, string environment)
    {
        var fields = new List<SetupField>();
        var slash = repository?.LastIndexOf('/') ?? -1;
        var owner = slash > 0 ? repository[..slash] : repository;
        var project = slash > 0 ? repository[(slash + 1)..] : null;

        if (registry == PackageRegistry.Pub)
        {
            fields.Add(new SetupField("repositoryPath", repository));
            fields.Add(new SetupField("tagPattern", "v{{version}}"));
        }
        else if (provider == CiProvider.GitLabCi)
        {
            fields.Add(new SetupField("namespace", owner));
            fields.Add(new SetupField("project", project));
            fields.Add(new SetupField("pipelineFile", workflow));
        }
        else
        {
            fields.Add(new SetupField("owner", owner));
            fields.Add(new SetupField("repository", project));
            fields.Add(new SetupField("workflow", workflow));
        }

        if (!string.IsNullOrEmpty(environment))
            fields.Add(new SetupField("environment", environment));

        return fields;
    }

    /// <summary>
    /// The latest release against the setup: the repository (and the workflow file, where the provenance names it) of the
    /// latest version's provenance must be the setup's.
    /// </summary>
    public static PublisherCheck Check(TrackedPackage package)
    {
        if (package.SetupProvider is not CiProvider provider || string.IsNullOrEmpty(package.SetupRepository))
            return new PublisherCheck(PublisherCheckStatus.NotConfigured, null, null, null, null, package.LatestVersion);

        var host = provider == CiProvider.GitLabCi ? "https://gitlab.com/" : "https://github.com/";
        var expectedRepository = host + package.SetupRepository;
        var expectedWorkflow = WorkflowPath(provider, package.SetupWorkflow);
        var observedRepository = package.HasProvenance ? package.SourceRepository : null;
        var observedWorkflow = package.HasProvenance ? package.LatestWorkflow : null;
        var status = PublisherCheckStatus.Match;

        if (!RegistryNames.SupportsProvenance(package.Registry))
            status = PublisherCheckStatus.NotVerifiable;
        else if (!package.HasProvenance)
            status = PublisherCheckStatus.NoProvenance;
        else if (observedRepository == null)
            status = PublisherCheckStatus.NotVerifiable;
        else if (!SameRepository(observedRepository, expectedRepository) || (observedWorkflow != null && !SameWorkflow(provider, observedWorkflow, package.SetupWorkflow)))
            status = PublisherCheckStatus.Mismatch;

        return new PublisherCheck(status, expectedRepository, expectedWorkflow, observedRepository, observedWorkflow, package.LatestVersion);
    }

    private static bool SameRepository(string observed, string expected) => string.Equals(Comparable(observed), Comparable(expected), StringComparison.OrdinalIgnoreCase);

    private static string Comparable(string url)
    {
        var value = url.Trim().TrimEnd('/');
        var scheme = value.IndexOf("://", StringComparison.Ordinal);

        value = scheme >= 0 ? value[(scheme + 3)..] : value;

        return value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
    }

    // GitHub provenance names the workflow path (.github/workflows/x.yml, npm) or the file (x.yml, PyPI); GitLab the CI file.
    private static bool SameWorkflow(CiProvider provider, string observed, string expected)
    {
        var observedFile = provider == CiProvider.GitLabCi ? observed.TrimStart('/') : observed[(observed.LastIndexOf('/') + 1)..];
        var at = observedFile.IndexOf('@', StringComparison.Ordinal);

        observedFile = at > 0 ? observedFile[..at] : observedFile;

        return string.Equals(observedFile, expected.TrimStart('/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A ready to copy CI file for <paramref name="registry"/> and <paramref name="provider"/> that publishes on a <c>v*</c>
    /// tag with OIDC (no stored token) and provenance where the registry records it; null when the registry has no trusted
    /// publishing from that provider. Text only: storeasy never commits it.
    /// </summary>
    public static string WorkflowText(PackageRegistry registry, CiProvider provider, string environment, string registryUser)
    {
        if (!Providers(registry).Contains(provider))
            return null;

        var yaml = new Yaml();

        if (provider == CiProvider.GitLabCi)
        {
            GitLabJob(yaml, registry, environment);
            return yaml.ToString();
        }

        yaml.Line(0, $"name: Publish to {RegistryNames.DisplayName(registry)}")
            .Line(0, "on:")
            .Line(1, "push:")
            .Line(2, "tags:")
            .Line(3, registry == PackageRegistry.Pub ? "- 'v[0-9]+.[0-9]+.[0-9]+'" : "- 'v*'");

        if (registry == PackageRegistry.Pub)
        {
            PubJob(yaml, environment);
            return yaml.ToString();
        }

        yaml.Line(0, "permissions:")
            .Line(1, "contents: read")
            .Line(0, "jobs:")
            .Line(1, "publish:")
            .Line(2, "runs-on: ubuntu-latest");

        if (!string.IsNullOrEmpty(environment))
            yaml.Line(2, $"environment: {environment}");

        yaml.Line(2, "permissions:")
            .Line(3, "id-token: write  # OIDC for trusted publishing")
            .Line(3, registry == PackageRegistry.RubyGems ? "contents: write" : "contents: read")
            .Line(2, "steps:")
            .Line(3, "- uses: actions/checkout@v5")
            .Line(4, "with:")
            .Line(5, "persist-credentials: false");

        GitHubSteps(yaml, registry, registryUser);

        return yaml.ToString();
    }

    private static void GitHubSteps(Yaml yaml, PackageRegistry registry, string registryUser)
    {
        switch (registry)
        {
            case PackageRegistry.Npm:
                yaml.Line(3, "- uses: actions/setup-node@v6")
                    .Line(4, "with:")
                    .Line(5, "node-version: '24'")
                    .Line(5, "registry-url: 'https://registry.npmjs.org'")
                    .Line(3, "- run: npm ci")
                    .Line(3, "# npm 11.5.1 or later publishes with trusted publishing and adds provenance on its own.")
                    .Line(3, "- run: npm publish --provenance --access public");
                break;
            case PackageRegistry.PyPI:
                yaml.Line(3, "- uses: actions/setup-python@v6")
                    .Line(4, "with:")
                    .Line(5, "python-version: '3.x'")
                    .Line(3, "- run: python -m pip install build && python -m build")
                    .Line(3, "- uses: pypa/gh-action-pypi-publish@release/v1")
                    .Line(4, "with:")
                    .Line(5, "attestations: true");
                break;
            case PackageRegistry.Crates:
                yaml.Line(3, "- uses: rust-lang/crates-io-auth-action@v1")
                    .Line(4, "id: auth")
                    .Line(3, "- run: cargo publish")
                    .Line(4, "env:")
                    .Line(5, "CARGO_REGISTRY_TOKEN: ${{ steps.auth.outputs.token }}");
                break;
            case PackageRegistry.NuGet:
                yaml.Line(3, "- uses: actions/setup-dotnet@v5")
                    .Line(4, "with:")
                    .Line(5, "dotnet-version: '10.0.x'")
                    .Line(3, "- run: dotnet pack --configuration Release --output artifacts")
                    .Line(3, "- uses: NuGet/login@v1")
                    .Line(4, "id: login")
                    .Line(4, "with:")
                    .Line(5, $"user: {(string.IsNullOrEmpty(registryUser) ? "your-nuget-profile-name" : registryUser)}")
                    .Line(3, "- run: dotnet nuget push artifacts/*.nupkg --api-key ${{ steps.login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json");
                break;
            default:
                yaml.Line(3, "- uses: ruby/setup-ruby@v1")
                    .Line(4, "with:")
                    .Line(5, "bundler-cache: true")
                    .Line(5, "ruby-version: ruby")
                    .Line(3, "- uses: rubygems/release-gem@v1");
                break;
        }
    }

    private static void PubJob(Yaml yaml, string environment)
    {
        yaml.Line(0, "jobs:")
            .Line(1, "publish:")
            .Line(2, "permissions:")
            .Line(3, "id-token: write  # OIDC for automated publishing")
            .Line(2, "uses: dart-lang/setup-dart/.github/workflows/publish.yml@v1");

        if (!string.IsNullOrEmpty(environment))
            yaml.Line(2, "with:").Line(3, $"environment: {environment}");
    }

    private static void GitLabJob(Yaml yaml, PackageRegistry registry, string environment)
    {
        var npm = registry == PackageRegistry.Npm;

        yaml.Line(0, $"publish-{(npm ? "npm" : "pypi")}:")
            .Line(1, npm ? "image: node:24" : "image: python:3-slim")
            .Line(1, "rules:")
            .Line(2, "- if: $CI_COMMIT_TAG =~ /^v/");

        if (!string.IsNullOrEmpty(environment))
            yaml.Line(1, $"environment: {environment}");

        yaml.Line(1, "id_tokens:");

        if (npm)
        {
            yaml.Line(2, "NPM_ID_TOKEN:").Line(3, "aud: \"npm:registry.npmjs.org\"")
                .Line(2, "SIGSTORE_ID_TOKEN:").Line(3, "aud: sigstore")
                .Line(1, "script:")
                .Line(2, "- npm ci")
                .Line(2, "- npm publish --provenance --access public");
        }
        else
        {
            yaml.Line(2, "PYPI_ID_TOKEN:").Line(3, "aud: pypi")
                .Line(1, "script:")
                .Line(2, "- python -m pip install build twine")
                .Line(2, "- python -m build")
                .Line(2, "# twine uses trusted publishing when no token is given.")
                .Line(2, "- python -m twine upload dist/*");
        }
    }

    /// <summary>
    /// A tiny YAML writer: two spaces per level, LF line ends.
    /// </summary>
    private sealed class Yaml
    {
        private readonly StringBuilder _builder = new();

        public Yaml Line(int level, string text)
        {
            _builder.Append(' ', level * 2).Append(text).Append('\n');
            return this;
        }

        public override string ToString() => _builder.ToString();
    }
}
