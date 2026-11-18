namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A CI token in lists (design 13.8). Never carries the value.
/// </summary>
public class CiTokenDto
{
    /// <summary>
    /// Token id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Apps.
    /// </summary>
    public List<int> AppIds { get; set; } = [];

    /// <summary>
    /// App names.
    /// </summary>
    public List<string> AppNames { get; set; } = [];

    /// <summary>
    /// Every app.
    /// </summary>
    public bool AllApps { get; set; }

    /// <summary>
    /// Scopes.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Display prefix.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// UTC expiry.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Whether it has expired.
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// UTC last use.
    /// </summary>
    public DateTime? LastUseDate { get; set; }

    /// <summary>
    /// Client of the last use.
    /// </summary>
    public string LastUseSource { get; set; }

    /// <summary>
    /// Member who created it.
    /// </summary>
    public string CreatorUserName { get; set; }

    /// <summary>
    /// UTC creation.
    /// </summary>
    public DateTime? CreationDate { get; set; }
}
