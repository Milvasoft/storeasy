namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Result of adding an MSI/EXE product: the record is confirmed by the sync job (follow <see cref="JobId"/>).
/// </summary>
public class MsProductAddedDto
{
    /// <summary>
    /// storeasy app the record was added to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// The new store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The sync job that confirms the product.
    /// </summary>
    public long JobId { get; set; }
}
