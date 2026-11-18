using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// TestFlight tester state. Values are explicit and never reused.
/// </summary>
public enum BetaTesterState : byte
{
    /// <summary>Added but not invited.</summary>
    [Description("Not invited")]
    NotInvited = 1,

    /// <summary>Invited.</summary>
    [Description("Invited")]
    Invited = 2,

    /// <summary>Accepted the invitation.</summary>
    [Description("Accepted")]
    Accepted = 3,

    /// <summary>Installed a build.</summary>
    [Description("Installed")]
    Installed = 4,

    /// <summary>Access revoked.</summary>
    [Description("Revoked")]
    Revoked = 5,
}
