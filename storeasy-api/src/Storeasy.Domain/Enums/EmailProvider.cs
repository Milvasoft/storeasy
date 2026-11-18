using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How e-mails leave the application (<c>Storeasy:Email:Provider</c>).
/// </summary>
public enum EmailProvider : byte
{
    /// <summary>
    /// An SMTP server (self-host).
    /// </summary>
    [Description("SMTP")]
    Smtp = 1,

    /// <summary>
    /// The Notifike notification API (Milvasoft hosting).
    /// </summary>
    [Description("Notifike")]
    Notifike = 2,

    /// <summary>
    /// Nothing is sent, the e-mail is only logged. Development only.
    /// </summary>
    [Description("Log")]
    Log = 3,
}
