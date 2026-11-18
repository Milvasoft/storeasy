namespace Storeasy.Application.Dtos.AssistedStepDtos;

/// <summary>
/// An assisted step of an app.
/// </summary>
public class AssistedStepListDto
{
    /// <summary>
    /// Step id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Definition key (catalog rule id or built in key).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Catalog rule, when the step comes from one.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Title in the language of the request.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Progress.
    /// </summary>
    public AssistedStepStatus Status { get; set; }

    /// <summary>
    /// Deadline from the catalog.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// UTC instant it was recorded as done or skipped.
    /// </summary>
    public DateTime? LastDoneDate { get; set; }

    /// <summary>
    /// Who recorded it.
    /// </summary>
    public string LastDoneByUserName { get; set; }

    /// <summary>
    /// Where the result came from.
    /// </summary>
    public ActionChannel? Channel { get; set; }
}
