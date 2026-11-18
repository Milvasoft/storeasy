using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kind of a bearer token storeasy issues (tools/CONTRACT.md section 7). Values are explicit and never reused.
/// </summary>
public enum TokenKind : byte
{
    /// <summary>
    /// A personal access token (sea_), bound to a user and an organization.
    /// </summary>
    [Description("Personal access token")]
    PersonalAccessToken = 1,

    /// <summary>
    /// A CI token (sci_), bound to an organization and some apps, not to a person.
    /// </summary>
    [Description("CI token")]
    CiToken = 2,

    /// <summary>
    /// A 15 minute upload token (sut_) issued by the OIDC exchange for one app.
    /// </summary>
    [Description("Upload token")]
    UploadToken = 3,
}
