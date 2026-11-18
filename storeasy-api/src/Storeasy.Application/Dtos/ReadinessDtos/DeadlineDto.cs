namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// The next deadline of an app.
/// </summary>
public class DeadlineDto
{
    /// <summary>
    /// Day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Catalog rule.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Title in the language of the request.
    /// </summary>
    public string Title { get; set; }
}
