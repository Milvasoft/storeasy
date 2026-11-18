using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// An identity provider a user can sign in with besides the password. Values are explicit and never reused.
/// </summary>
public enum ExternalLoginProvider : byte
{
    /// <summary>
    /// Google (OpenID Connect, authorization code with PKCE).
    /// </summary>
    [Description("Google")]
    Google = 1,
}
