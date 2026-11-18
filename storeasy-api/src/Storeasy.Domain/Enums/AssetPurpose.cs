using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a stored file is for; decides its folder and size limit. Values are explicit and never reused.
/// </summary>
public enum AssetPurpose : byte
{
    /// <summary>
    /// A screenshot or preview image for a store listing.
    /// </summary>
    [Description("Screenshot")]
    Screenshot = 1,

    /// <summary>
    /// An app icon or listing graphic.
    /// </summary>
    [Description("Graphic")]
    Graphic = 2,

    /// <summary>
    /// An image produced by the mockup studio.
    /// </summary>
    [Description("Mockup output")]
    MockupOutput = 3,

    /// <summary>
    /// A build package (IPA, AAB, MSIX) from a CI pipeline or an upload.
    /// </summary>
    [Description("Build artifact")]
    BuildArtifact = 4,
}
