using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of an organization invitation as its preview reports it.
/// </summary>
public enum InvitationStatus : byte
{
    /// <summary>
    /// Can be accepted.
    /// </summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>
    /// Its seven days passed.
    /// </summary>
    [Description("Expired")]
    Expired = 2,

    /// <summary>
    /// A member manager revoked it (or a newer invitation to the same address replaced it).
    /// </summary>
    [Description("Revoked")]
    Revoked = 3,

    /// <summary>
    /// Already used.
    /// </summary>
    [Description("Accepted")]
    Accepted = 4,
}
