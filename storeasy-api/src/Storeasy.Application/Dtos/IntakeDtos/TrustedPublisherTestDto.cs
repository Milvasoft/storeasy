namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// Result of checking an OIDC token against one rule without issuing anything (design 13.7).
/// </summary>
public class TrustedPublisherTestDto
{
    /// <summary>
    /// Whether the token passed signature, expiry and issuer checks.
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    /// Whether every claim matched.
    /// </summary>
    public bool Matched { get; set; }

    /// <summary>
    /// Why the token is not valid.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Claim by claim comparison.
    /// </summary>
    public List<ClaimComparisonDto> Claims { get; set; } = [];
}
