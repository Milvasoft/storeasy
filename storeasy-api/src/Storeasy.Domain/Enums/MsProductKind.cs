using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Which Microsoft Store API manages a product.
/// </summary>
public enum MsProductKind : byte
{
    /// <summary>
    /// MSIX or UWP app, managed by the Microsoft Store submission API (manage.devcenter.microsoft.com).
    /// </summary>
    [Description("MSIX or UWP app")]
    Msix = 1,

    /// <summary>
    /// MSI or EXE app, managed by the Microsoft Store submission API for MSI/EXE (api.store.microsoft.com).
    /// </summary>
    [Description("MSI or EXE app")]
    MsiExe = 2,
}
