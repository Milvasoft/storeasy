using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How an approved App Store version is released.
/// </summary>
public enum ReleaseType : byte
{
    /// <summary>
    /// The developer releases it.
    /// </summary>
    [Description("Manual")]
    Manual = 1,

    /// <summary>
    /// Released when approved.
    /// </summary>
    [Description("After approval")]
    AfterApproval = 2,

    /// <summary>
    /// Released on a date (or on approval, if later).
    /// </summary>
    [Description("Scheduled")]
    Scheduled = 3,
}
