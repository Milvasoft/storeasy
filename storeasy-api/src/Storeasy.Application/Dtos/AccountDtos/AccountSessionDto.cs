namespace Storeasy.Application.Dtos.AccountDtos;

/// <summary>
/// One signed in device of the caller.
/// </summary>
public class AccountSessionDto
{
    /// <summary>
    /// Session id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Device id the client sent at sign in.
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// Browser or client.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Address of the last sign in or token refresh.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// UTC instant of the last sign in or token refresh (a session refreshes its token while it is in use).
    /// </summary>
    public DateTime? LastSeenDate { get; set; }

    /// <summary>
    /// UTC instant the session ends unless it is refreshed.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Whether this is the session of the request.
    /// </summary>
    public bool IsCurrent { get; set; }
}
