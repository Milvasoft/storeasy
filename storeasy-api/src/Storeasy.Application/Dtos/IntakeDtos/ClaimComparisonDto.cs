namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// One claim of a dry run exchange: what the rule expects and what the token carries.
/// </summary>
public class ClaimComparisonDto
{
    /// <summary>
    /// Claim name.
    /// </summary>
    public string Claim { get; set; }

    /// <summary>
    /// Expected value or pattern.
    /// </summary>
    public string Expected { get; set; }

    /// <summary>
    /// Value in the token.
    /// </summary>
    public string Actual { get; set; }

    /// <summary>
    /// Whether it matches.
    /// </summary>
    public bool Matches { get; set; }
}
