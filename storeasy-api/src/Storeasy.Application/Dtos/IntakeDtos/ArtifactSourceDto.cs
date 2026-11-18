namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A pull source (design 13.19). The secret is never returned after it was set.
/// </summary>
public class ArtifactSourceDto
{
    /// <summary>
    /// Source id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// EAS or generic webhook.
    /// </summary>
    public ArtifactPullSourceKind Kind { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Webhook path to give the build service.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Allowed download domains.
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// Platforms taken.
    /// </summary>
    public List<ArtifactPlatform> Platforms { get; set; } = [];

    /// <summary>
    /// Whether APK builds are taken.
    /// </summary>
    public bool IncludeApk { get; set; }

    /// <summary>
    /// UTC instant the secret was set.
    /// </summary>
    public DateTime SecretSetDate { get; set; }

    /// <summary>
    /// UTC last call.
    /// </summary>
    public DateTime? LastCallDate { get; set; }

    /// <summary>
    /// Outcome of the last call.
    /// </summary>
    public string LastCallResult { get; set; }
}
