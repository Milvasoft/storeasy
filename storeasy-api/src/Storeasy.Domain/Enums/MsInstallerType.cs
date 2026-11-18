using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Installer type of an MSI/EXE package.
/// </summary>
public enum MsInstallerType : byte
{
    /// <summary>
    /// EXE installer.
    /// </summary>
    [Description("EXE")]
    Exe = 1,

    /// <summary>
    /// MSI installer.
    /// </summary>
    [Description("MSI")]
    Msi = 2,
}
