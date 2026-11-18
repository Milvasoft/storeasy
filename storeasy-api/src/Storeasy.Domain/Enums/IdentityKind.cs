using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How a package upload was authenticated. Values are explicit and never reused.
/// </summary>
public enum IdentityKind : byte
{
    /// <summary>
    /// A trusted publisher (OIDC exchange).
    /// </summary>
    [Description("OIDC")]
    Oidc = 1,

    /// <summary>
    /// A CI token.
    /// </summary>
    [Description("CI token")]
    CiToken = 2,

    /// <summary>
    /// A member (web, personal access token).
    /// </summary>
    [Description("User")]
    User = 3,
}
