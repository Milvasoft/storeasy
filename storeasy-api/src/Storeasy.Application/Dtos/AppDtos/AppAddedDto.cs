namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// Result of adding an app by hand: the record is confirmed by the sync job (follow <see cref="JobId"/>); a record the
/// store refuses is removed by that job.
/// </summary>
public class AppAddedDto
{
    /// <summary>
    /// storeasy app the record was added to (an existing one when the identifiers matched).
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// The new store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The sync job that confirms the record and reads its builds.
    /// </summary>
    public long JobId { get; set; }
}
