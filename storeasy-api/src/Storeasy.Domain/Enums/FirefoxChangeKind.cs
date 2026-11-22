using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a Firefox Add-ons change plan writes. Each kind is one family of AMO API v5 writes; nothing is sent before the plan
/// is approved.
/// </summary>
public enum FirefoxChangeKind : byte
{
    /// <summary>
    /// Listing texts per locale (name, summary, description), homepage, support e-mail and address, categories
    /// (<c>PATCH /addons/addon/{id}/</c>).
    /// </summary>
    [Description("Listing")]
    Listing = 1,

    /// <summary>
    /// A new version: the package is uploaded (<c>POST /addons/upload/</c>), validated by AMO, then the version is created on
    /// the listed or unlisted channel with its release notes, notes to the reviewer, license and source code.
    /// </summary>
    [Description("New version")]
    Version = 2,

    /// <summary>
    /// Previews (screenshots): add images, remove previews (<c>/addons/addon/{id}/previews/</c>).
    /// </summary>
    [Description("Previews")]
    Previews = 3,

    /// <summary>
    /// Replace the icon (<c>PATCH /addons/addon/{id}/</c> with an <c>icon</c> file).
    /// </summary>
    [Description("Icon")]
    Icon = 4,

    /// <summary>
    /// Release notes of an existing version (<c>PATCH /addons/addon/{id}/versions/{id}/</c>).
    /// </summary>
    [Description("Release notes")]
    ReleaseNotes = 5,
}
