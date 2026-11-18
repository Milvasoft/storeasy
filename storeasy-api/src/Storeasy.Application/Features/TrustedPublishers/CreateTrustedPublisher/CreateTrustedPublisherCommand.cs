using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TrustedPublishers.CreateTrustedPublisher;

/// <summary>
/// Adds a trusted publisher rule to an app (design 13.5, 13.6). GitHub: repository, branch, optional environment and
/// workflow. Azure DevOps: organization, project, service connection (the token has no branch). GitLab: project path,
/// branch, optional environment and self-managed issuer.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record CreateTrustedPublisherCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// CI provider.
    /// </summary>
    public OidcProvider Provider { get; set; }

    /// <summary>
    /// GitHub owner/repo or GitLab group/project.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// GitHub workflow file (optional).
    /// </summary>
    public string Workflow { get; set; }

    /// <summary>
    /// Branch pattern (main, release/*).
    /// </summary>
    public string Branch { get; set; }

    /// <summary>
    /// Environment (optional, recommended).
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// Azure DevOps organization.
    /// </summary>
    public string Organization { get; set; }

    /// <summary>
    /// Azure DevOps project.
    /// </summary>
    public string Project { get; set; }

    /// <summary>
    /// Azure DevOps service connection.
    /// </summary>
    public string ServiceConnection { get; set; }

    /// <summary>
    /// Self-managed GitLab URL (https), empty for gitlab.com.
    /// </summary>
    public string Issuer { get; set; }
}
