namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// How a session was authenticated.
/// </summary>
public class ArtifactIdentityDto
{
    /// <summary>
    /// Identity kind.
    /// </summary>
    public IdentityKind? Kind { get; set; }

    /// <summary>
    /// Trusted publisher rule (OIDC).
    /// </summary>
    public int? TrustedPublisherId { get; set; }

    /// <summary>
    /// CI token prefix.
    /// </summary>
    public string CiTokenPrefix { get; set; }

    /// <summary>
    /// Member.
    /// </summary>
    public string UserName { get; set; }
}
