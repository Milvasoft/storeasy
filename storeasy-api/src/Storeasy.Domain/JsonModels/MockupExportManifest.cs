using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// Result of a mockup export job (the job's <c>Result</c>, also <c>manifest.json</c> in the zip): the produced images as
/// sets by store, screenshot class and locale, in slot order. This is the hand over to the screenshot upload plan: one
/// <see cref="MockupExportSet"/> is one screenshot set of a store (an App Store display type in a localization, a Play
/// image type in a language, a Microsoft Store device family in a language). Documented in storeasy-api/docs/studio.md;
/// <see cref="SchemaVersion"/> changes when a field changes meaning.
/// </summary>
public class MockupExportManifest
{
    /// <summary>
    /// Version of this shape.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Export job.
    /// </summary>
    public long JobId { get; set; }

    /// <summary>
    /// Template the images were drawn from.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// App of the template.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Template version that was drawn.
    /// </summary>
    public int TemplateVersion { get; set; }

    /// <summary>
    /// Rules catalog version the classes and sizes came from.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// UTC instant the export finished.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Asset of the zip with every image and this manifest.
    /// </summary>
    public long? ZipAssetId { get; set; }

    /// <summary>
    /// Images produced.
    /// </summary>
    public int ImageCount { get; set; }

    /// <summary>
    /// Bytes of the images produced.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Image sets by store, class and locale.
    /// </summary>
    public List<MockupExportSet> Sets { get; set; } = [];

    /// <summary>
    /// Images that could not be produced (they are not in any set).
    /// </summary>
    public List<MockupExportIssue> Failures { get; set; } = [];

    /// <summary>
    /// Warnings of produced images.
    /// </summary>
    public List<MockupExportIssue> Warnings { get; set; } = [];
}

/// <summary>
/// The images of one store screenshot set.
/// </summary>
public class MockupExportSet
{
    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Screenshot class key (= rules catalog id, e.g. <c>apple.screenshots.iphone-dynamic-island-medium</c>).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// The store's own key of the class (App Store display type such as <c>APP_IPHONE_61</c>, Play image type such as
    /// <c>phoneScreenshots</c>), the <c>classKey</c> of <c>POST screenshots/plan</c>; null when the store API has no key
    /// for the class yet (iPhone Duo, Microsoft Store).
    /// </summary>
    public string StoreClassKey { get; set; }

    /// <summary>
    /// Locale tag.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Pixel width of every image of the set.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Pixel height of every image of the set.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Images in slot order.
    /// </summary>
    public List<MockupExportImage> Images { get; set; } = [];
}

/// <summary>
/// One produced image.
/// </summary>
public class MockupExportImage
{
    /// <summary>
    /// Slot position, from 1 (the display order in the store).
    /// </summary>
    public int SlotOrder { get; set; }

    /// <summary>
    /// The PNG asset (purpose MockupOutput, uploaded, no alpha channel).
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the PNG.
    /// </summary>
    public string Sha256 { get; set; }

    /// <summary>
    /// Lower case hex MD5 of the PNG (App Store Connect compares checksums with it).
    /// </summary>
    public string Md5 { get; set; }

    /// <summary>
    /// Path inside the zip.
    /// </summary>
    public string ZipPath { get; set; }
}

/// <summary>
/// A failure or warning of one image.
/// </summary>
public class MockupExportIssue
{
    /// <summary>
    /// Screenshot class key.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Locale tag.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Slot position.
    /// </summary>
    public int SlotOrder { get; set; }

    /// <summary>
    /// Code (see <see cref="Studio.MockupWarningCodes"/>).
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// English detail.
    /// </summary>
    public string Message { get; set; }
}
