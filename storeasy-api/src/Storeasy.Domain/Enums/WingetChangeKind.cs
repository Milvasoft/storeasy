using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a winget change plan sends to winget-pkgs. Each kind is one pull request with one manifest set, as the repository
/// requires (one package version per pull request).
/// </summary>
public enum WingetChangeKind : byte
{
    /// <summary>
    /// A new version: version, default locale, installer and locale manifests generated from installers storeasy knows
    /// (the Microsoft Store MSI/EXE packages of the same app) or given in the plan.
    /// </summary>
    [Description("New version")]
    NewVersion = 1,

    /// <summary>
    /// Locale text edits (the winget "listing") on an existing version: its default locale and locale manifests are
    /// rewritten, the installer manifest is kept.
    /// </summary>
    [Description("Locale texts")]
    Listing = 2,
}
