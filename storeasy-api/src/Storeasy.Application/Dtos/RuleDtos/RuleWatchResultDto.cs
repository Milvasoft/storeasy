namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// Result of reading the watched sources now.
/// </summary>
public class RuleWatchResultDto
{
    /// <summary>Sources read.</summary>
    public int Checked { get; set; }

    /// <summary>Sources whose content changed.</summary>
    public int Changed { get; set; }

    /// <summary>Sources that could not be read.</summary>
    public int Unreachable { get; set; }

    /// <summary>Proposals opened.</summary>
    public int ProposalsOpened { get; set; }
}
