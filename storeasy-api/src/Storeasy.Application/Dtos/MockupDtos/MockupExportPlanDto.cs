namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// What an export of a template would produce: the screenshot classes of the app's stores from the rules catalog with
/// their requirement, the sizes that were refused, and the image count. Nothing is written.
/// </summary>
public class MockupExportPlanDto
{
    /// <summary>
    /// Template.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Rules catalog version the classes came from.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Locales to export.
    /// </summary>
    public List<string> Locales { get; set; }

    /// <summary>
    /// Classes, required first within each store.
    /// </summary>
    public List<MockupExportPlanItemDto> Items { get; set; }

    /// <summary>
    /// Requested sizes a class does not accept.
    /// </summary>
    public List<MockupExportRejectedDto> Rejected { get; set; }

    /// <summary>
    /// Images the selected classes produce.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Plan warnings (more slots than a store accepts, missing captions).
    /// </summary>
    public List<MockupWarningDto> Warnings { get; set; }
}

/// <summary>
/// One screenshot class in an export plan.
/// </summary>
public class MockupExportPlanItemDto
{
    /// <summary>
    /// Class key (rules catalog id).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// English label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Turkish label.
    /// </summary>
    public string LabelTr { get; set; }

    /// <summary>
    /// Pixel width that will be rendered.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Pixel height that will be rendered.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Frame the class is drawn with.
    /// </summary>
    public MockupFrameKind Frame { get; set; }

    /// <summary>
    /// How much the store wants the class.
    /// </summary>
    public MockupRequirement Status { get; set; }

    /// <summary>
    /// Date the class becomes required.
    /// </summary>
    public DateOnly? RequiredFrom { get; set; }

    /// <summary>
    /// Whether the export produces the class.
    /// </summary>
    public bool Selected { get; set; }

    /// <summary>
    /// Images per locale.
    /// </summary>
    public int PerLocaleCount { get; set; }

    /// <summary>
    /// Catalog note of the requirement.
    /// </summary>
    public string Note { get; set; }
}

/// <summary>
/// A requested size a class does not accept.
/// </summary>
public class MockupExportRejectedDto
{
    /// <summary>
    /// Class key.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Requested width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Requested height.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// English reason.
    /// </summary>
    public string Reason { get; set; }
}
