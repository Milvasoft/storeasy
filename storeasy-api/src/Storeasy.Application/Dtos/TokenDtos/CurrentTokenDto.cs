namespace Storeasy.Application.Dtos.TokenDtos;

/// <summary>
/// The token of the request (tools/CONTRACT.md 2.1). Never carries the token value.
/// </summary>
public class CurrentTokenDto
{
    /// <summary>
    /// Kind of the token.
    /// </summary>
    public TokenKind Kind { get; set; }

    /// <summary>
    /// Name of the token.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Display prefix.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Organization the token is bound to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name of that organization.
    /// </summary>
    public string OrganizationName { get; set; }

    /// <summary>
    /// Owner of a personal access token; null for CI and upload tokens.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Whether a personal access token is read only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Scopes of a CI or upload token.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Apps a CI or upload token is limited to.
    /// </summary>
    public List<int> AppIds { get; set; } = [];

    /// <summary>
    /// Whether the token covers every app.
    /// </summary>
    public bool AllApps { get; set; }

    /// <summary>
    /// UTC instant the token stops working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }
}
