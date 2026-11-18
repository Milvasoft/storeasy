using System.Reflection;

namespace Storeasy.Application.Utils.Models;

/// <summary>
/// What build is running (configuration section <see cref="SectionName"/>, set by the image build). Bound once and
/// registered as a singleton.
/// </summary>
public class BuildOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Build";

    /// <summary>
    /// Source commit of the build; when empty, the part after <c>+</c> of the informational version, if any.
    /// </summary>
    public string Commit { get; set; }

    /// <summary>
    /// Version of the application (the informational version without the commit suffix).
    /// </summary>
    public string Version => InformationalVersion.Split('+')[0];

    /// <summary>
    /// The commit in force: <see cref="Commit"/>, or the one stamped into the informational version.
    /// </summary>
    public string ResolvedCommit => !string.IsNullOrWhiteSpace(Commit) ? Commit.Trim() : InformationalVersion.Contains('+') ? InformationalVersion[(InformationalVersion.IndexOf('+') + 1)..] : null;

    private static string InformationalVersion => typeof(BuildOptions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
}
