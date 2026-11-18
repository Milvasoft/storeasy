using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Outcome of the last OIDC exchange against a trusted publisher rule. Values are explicit and never reused.
/// </summary>
public enum OidcExchangeResult : byte
{
    /// <summary>
    /// An upload token was issued.
    /// </summary>
    [Description("Succeeded")]
    Succeeded = 1,

    /// <summary>
    /// The token was valid but its claims did not match the rule.
    /// </summary>
    [Description("Claim mismatch")]
    ClaimMismatch = 2,

    /// <summary>
    /// The token failed validation (signature, expiry, audience, issuer).
    /// </summary>
    [Description("Invalid token")]
    InvalidToken = 3,
}
