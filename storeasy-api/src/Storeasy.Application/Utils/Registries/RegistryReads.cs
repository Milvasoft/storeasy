namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// A package as its registry's public API describes it.
/// </summary>
public sealed class RegistryPackageRead
{
    /// <summary>
    /// Package name as the registry spells it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The registry's latest version.
    /// </summary>
    public string LatestVersion { get; set; }

    /// <summary>
    /// Repository (or project) address the package declares.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Every version the registry lists.
    /// </summary>
    public List<RegistryVersionRead> Versions { get; set; } = [];

    /// <summary>
    /// The read version named <see cref="LatestVersion"/>, or null.
    /// </summary>
    public RegistryVersionRead Latest => Versions.Find(v => v.Version == LatestVersion);
}

/// <summary>
/// One version of a <see cref="RegistryPackageRead"/>.
/// </summary>
public sealed class RegistryVersionRead
{
    /// <summary>
    /// Version string.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// UTC instant the version was published; null when the registry does not say.
    /// </summary>
    public DateTime? Published { get; set; }

    /// <summary>
    /// Yanked (crates.io, PyPI).
    /// </summary>
    public bool Yanked { get; set; }

    /// <summary>
    /// Unlisted (NuGet).
    /// </summary>
    public bool Unlisted { get; set; }

    /// <summary>
    /// Deprecated (npm, NuGet).
    /// </summary>
    public bool Deprecated { get; set; }

    /// <summary>
    /// Deprecation message or yank reason.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Whether the version has provenance; null when the version list does not say (PyPI: read per file).
    /// </summary>
    public bool? HasProvenance { get; set; }

    /// <summary>
    /// CI run that published the version, when the version list names it (crates.io trusted publishing).
    /// </summary>
    public string CiRunUrl { get; set; }

    /// <summary>
    /// Source repository of the trusted publisher, when the version list names it (crates.io trusted publishing).
    /// </summary>
    public string SourceRepository { get; set; }

    /// <summary>
    /// A distribution file of the version whose provenance can be read (PyPI).
    /// </summary>
    public string ArtifactFile { get; set; }
}

/// <summary>
/// Provenance of one version: where and by which CI run it was built.
/// </summary>
/// <param name="Repository">Source repository address.</param>
/// <param name="Workflow">CI workflow (path or file name).</param>
/// <param name="RunUrl">CI run address, when the provenance names it.</param>
public sealed record ProvenanceRead(string Repository, string Workflow, string RunUrl);

/// <summary>
/// A security advisory from OSV.dev.
/// </summary>
/// <param name="Id">Advisory id.</param>
/// <param name="Severity">Severity word (<c>LOW</c>, <c>MODERATE</c>, <c>HIGH</c>, <c>CRITICAL</c>) when known.</param>
/// <param name="Summary">One line summary.</param>
/// <param name="Url">Details page.</param>
public sealed record RegistryAdvisory(string Id, string Severity, string Summary, string Url);

/// <summary>
/// One OSV.dev query: a package version.
/// </summary>
/// <param name="Registry">The registry.</param>
/// <param name="Name">Package name.</param>
/// <param name="Version">Version.</param>
public sealed record AdvisoryQuery(PackageRegistry Registry, string Name, string Version);
