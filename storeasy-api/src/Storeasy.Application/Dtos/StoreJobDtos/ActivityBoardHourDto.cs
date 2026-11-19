namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// One hour of one store on the activity page's 24 hour strip.
/// </summary>
public class ActivityBoardHourDto
{
    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// UTC start of the hour.
    /// </summary>
    public DateTime Hour { get; set; }

    /// <summary>
    /// Jobs that finished successfully in the hour.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Jobs that failed for good in the hour.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Store events (webhooks) received in the hour.
    /// </summary>
    public int Inbound { get; set; }
}
