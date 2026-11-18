namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A trusted publisher rule (design 13.4).
/// </summary>
public class TrustedPublisherDto
{
    /// <summary>
    /// Rule id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// CI provider.
    /// </summary>
    public OidcProvider Provider { get; set; }

    /// <summary>
    /// Repository or project path.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// GitHub workflow file.
    /// </summary>
    public string Workflow { get; set; }

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
    /// Branch pattern.
    /// </summary>
    public string Branch { get; set; }

    /// <summary>
    /// Environment.
    /// </summary>
    public string Environment { get; set; }

    /// <summary>
    /// Self-managed GitLab issuer.
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// Audience the token must carry.
    /// </summary>
    public string Audience { get; set; }

    /// <summary>
    /// The claims a matching token carries.
    /// </summary>
    public Dictionary<string, string> ExpectedClaims { get; set; } = [];

    /// <summary>
    /// UTC last exchange.
    /// </summary>
    public DateTime? LastExchangeDate { get; set; }

    /// <summary>
    /// Outcome of the last exchange.
    /// </summary>
    public OidcExchangeResult? LastExchangeResult { get; set; }

    /// <summary>
    /// CI run of the last exchange.
    /// </summary>
    public string LastExchangeRunUrl { get; set; }

    /// <summary>
    /// Claims of the last exchange (detail only).
    /// </summary>
    public List<Storeasy.Domain.Intake.OidcClaim> LastClaims { get; set; }

    /// <summary>
    /// Member who added the rule.
    /// </summary>
    public string CreatorUserName { get; set; }

    /// <summary>
    /// UTC creation.
    /// </summary>
    public DateTime? CreationDate { get; set; }
}
