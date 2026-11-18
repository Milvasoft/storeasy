using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// A trusted publisher rule of an app (docs/urun-plani.md 11.8, design 13.4-13.7): a CI run whose OIDC token comes from
/// <see cref="Provider"/> and carries the claims described here may exchange it for a 15 minute upload token. The raw
/// OIDC token is never stored; only the claims of the last exchange are kept for the diagnosis drawer.
/// </summary>
[Table(TableNames.TrustedPublishers)]
[Index(nameof(OrganizationId))]
[Index(nameof(AppId))]
public class TrustedPublisher : CreationAuditableEntity<int>
{
    /// <summary>
    /// Column length of the claim pattern fields.
    /// </summary>
    public const int FieldMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App the rule lets packages into.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// CI provider.
    /// </summary>
    public OidcProvider Provider { get; set; }

    /// <summary>
    /// GitHub <c>owner/repo</c> or GitLab <c>group/project</c> path.
    /// </summary>
    [MaxLength(FieldMaxLength)]
    public string Repository { get; set; }

    /// <summary>
    /// GitHub workflow file (<c>.github/workflows/release.yml</c>), optional.
    /// </summary>
    [MaxLength(300)]
    public string Workflow { get; set; }

    /// <summary>
    /// Branch pattern (<c>main</c>, <c>release/*</c>); not available for Azure DevOps.
    /// </summary>
    [MaxLength(FieldMaxLength)]
    public string Branch { get; set; }

    /// <summary>
    /// Deployment environment, optional.
    /// </summary>
    [MaxLength(100)]
    public string Environment { get; set; }

    /// <summary>
    /// Azure DevOps organization name.
    /// </summary>
    [MaxLength(100)]
    public string AzureOrganization { get; set; }

    /// <summary>
    /// Azure DevOps project.
    /// </summary>
    [MaxLength(100)]
    public string AzureProject { get; set; }

    /// <summary>
    /// Azure DevOps service connection name.
    /// </summary>
    [MaxLength(FieldMaxLength)]
    public string ServiceConnection { get; set; }

    /// <summary>
    /// Issuer of a self-managed GitLab (https URL); null for gitlab.com and the other providers.
    /// </summary>
    [MaxLength(300)]
    public string Issuer { get; set; }

    /// <summary>
    /// UTC instant of the last exchange attempt that named this app and matched this rule's provider.
    /// </summary>
    public DateTime? LastExchangeDate { get; set; }

    /// <summary>
    /// Outcome of that attempt.
    /// </summary>
    public OidcExchangeResult? LastExchangeResult { get; set; }

    /// <summary>
    /// CI run of that attempt.
    /// </summary>
    [MaxLength(500)]
    public string LastExchangeRunUrl { get; set; }

    /// <summary>
    /// Claims of that attempt (never the token itself).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<OidcClaim> LastClaims { get; set; }

    /// <summary>
    /// Member who added the rule.
    /// </summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// Name of that member.
    /// </summary>
    [MaxLength(256)]
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    public virtual App App { get; set; }
}
