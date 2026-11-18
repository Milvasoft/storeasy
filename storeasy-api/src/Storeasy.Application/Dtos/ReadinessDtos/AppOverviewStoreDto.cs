namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// Readiness of one store record in the organization overview.
/// </summary>
public class AppOverviewStoreDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the app.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Version checked.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// UTC instant of the last check; null when never checked.
    /// </summary>
    public DateTime? CheckedDate { get; set; }

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
    /// Keys of the blocking checks.
    /// </summary>
    public List<string> MissingKeys { get; set; }

    /// <summary>
    /// Why the store could not be read, when the refresh failed.
    /// </summary>
    public string Error { get; set; }
}
