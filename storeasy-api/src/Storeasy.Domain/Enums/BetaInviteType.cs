using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How a TestFlight tester joined. Values are explicit and never reused.
/// </summary>
public enum BetaInviteType : byte
{
    /// <summary>E-mail invitation.</summary>
    [Description("E-mail")]
    Email = 1,

    /// <summary>Public link.</summary>
    [Description("Public link")]
    PublicLink = 2,
}
