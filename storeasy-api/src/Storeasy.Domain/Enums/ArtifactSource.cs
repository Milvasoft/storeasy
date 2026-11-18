using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where a package came from. Values are explicit and never reused.
/// </summary>
public enum ArtifactSource : byte
{
    /// <summary>
    /// The GitHub Action.
    /// </summary>
    [Description("GitHub Actions")]
    GitHubActions = 1,

    /// <summary>
    /// The Azure DevOps task.
    /// </summary>
    [Description("Azure DevOps")]
    AzureDevOps = 2,

    /// <summary>
    /// GitLab CI.
    /// </summary>
    [Description("GitLab")]
    GitLab = 3,

    /// <summary>
    /// The storeasy CLI on a developer machine.
    /// </summary>
    [Description("CLI")]
    Cli = 4,

    /// <summary>
    /// The REST API directly.
    /// </summary>
    [Description("REST")]
    Rest = 5,

    /// <summary>
    /// Pulled after an EAS build webhook.
    /// </summary>
    [Description("EAS webhook")]
    Eas = 6,

    /// <summary>
    /// Pulled after a signed generic webhook.
    /// </summary>
    [Description("Webhook")]
    Webhook = 7,

    /// <summary>
    /// Uploaded in the web app or through MCP.
    /// </summary>
    [Description("Manual")]
    Manual = 8,

    /// <summary>
    /// Seen in the store by the build sync (uploaded by the CI itself).
    /// </summary>
    [Description("Sync")]
    Sync = 9,
}
