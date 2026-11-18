using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Authenticode check of an MSI/EXE installer.
/// </summary>
public enum MsSignatureStatus : byte
{
    /// <summary>
    /// Not checked yet.
    /// </summary>
    [Description("Not checked")]
    Unchecked = 1,

    /// <summary>
    /// The installer carries an Authenticode signature.
    /// </summary>
    [Description("Signed")]
    Signed = 2,

    /// <summary>
    /// No Authenticode signature was found; the Store rejects the installer.
    /// </summary>
    [Description("Unsigned")]
    Unsigned = 3,

    /// <summary>
    /// The installer could not be downloaded for the check.
    /// </summary>
    [Description("Could not be downloaded")]
    Unreachable = 4,
}
