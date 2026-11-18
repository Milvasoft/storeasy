using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// CI provider of a trusted publisher rule (OIDC token issuer family). Values are explicit and never reused.
/// </summary>
public enum OidcProvider : byte
{
    /// <summary>
    /// GitHub Actions (issuer https://token.actions.githubusercontent.com).
    /// </summary>
    [Description("GitHub")]
    GitHub = 1,

    /// <summary>
    /// Azure Pipelines workload identity federation (issuer https://vstoken.dev.azure.com/{organization id}).
    /// </summary>
    [Description("Azure DevOps")]
    AzureDevOps = 2,

    /// <summary>
    /// GitLab CI (gitlab.com or a self-managed instance).
    /// </summary>
    [Description("GitLab")]
    GitLab = 3,
}
