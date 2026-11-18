namespace Storeasy.Domain.JsonModels;

/// <summary>
/// The last readiness check of a store record, kept for the organization overview.
/// </summary>
public class ReadinessSummary
{
    /// <summary>
    /// UTC instant of the check.
    /// </summary>
    public DateTime CheckedDate { get; set; }

    /// <summary>
    /// Rules catalog version the check used.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Version the check looked at.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Checks that passed.
    /// </summary>
    public int Ok { get; set; }

    /// <summary>
    /// Checks that block the submission.
    /// </summary>
    public int Missing { get; set; }

    /// <summary>
    /// Non blocking remarks.
    /// </summary>
    public int Warning { get; set; }

    /// <summary>
    /// Assisted steps still open.
    /// </summary>
    public int Assisted { get; set; }

    /// <summary>
    /// Checks storeasy could not decide.
    /// </summary>
    public int Unknown { get; set; }

    /// <summary>
    /// Keys of the blocking checks.
    /// </summary>
    public List<string> MissingKeys { get; set; }
}
