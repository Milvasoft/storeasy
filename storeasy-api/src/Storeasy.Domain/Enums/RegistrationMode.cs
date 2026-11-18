using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Who may create an account on this instance (<c>Storeasy:Registration:Mode</c>).
/// </summary>
public enum RegistrationMode : byte
{
    /// <summary>
    /// Anyone may register.
    /// </summary>
    [Description("Open")]
    Open = 1,

    /// <summary>
    /// Only the holder of a valid organization invitation for the same e-mail may register.
    /// </summary>
    [Description("Invite only")]
    InviteOnly = 2,

    /// <summary>
    /// Nobody may register; accounts are created by an instance admin.
    /// </summary>
    [Description("Closed")]
    Closed = 3,
}
