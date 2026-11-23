using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// CI provider a trusted publishing setup of a tracked package publishes from.
/// </summary>
public enum CiProvider : byte
{
    /// <summary>
    /// GitHub Actions.
    /// </summary>
    [Description("GitHub Actions")]
    GitHubActions = 1,

    /// <summary>
    /// GitLab CI/CD (gitlab.com).
    /// </summary>
    [Description("GitLab CI/CD")]
    GitLabCi = 2,
}
