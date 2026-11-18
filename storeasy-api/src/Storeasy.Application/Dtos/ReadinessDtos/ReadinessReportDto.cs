namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// What is missing before a submission of one store record (docs/urun-plani.md 11.1).
/// </summary>
public class ReadinessReportDto
{
    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Version checked.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Rules catalog version used.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// UTC instant of the check.
    /// </summary>
    public DateTime CheckedDate { get; set; }

    /// <summary>
    /// Whether nothing blocks the submission.
    /// </summary>
    public bool CanSubmit { get; set; }

    /// <summary>
    /// Passed checks.
    /// </summary>
    public int Ok { get; set; }

    /// <summary>
    /// Blocking checks.
    /// </summary>
    public int Missing { get; set; }

    /// <summary>
    /// Remarks.
    /// </summary>
    public int Warning { get; set; }

    /// <summary>
    /// Open assisted steps.
    /// </summary>
    public int Assisted { get; set; }

    /// <summary>
    /// Undecided checks.
    /// </summary>
    public int Unknown { get; set; }

    /// <summary>
    /// Checks.
    /// </summary>
    public List<ReadinessItemDto> Items { get; set; }
}
