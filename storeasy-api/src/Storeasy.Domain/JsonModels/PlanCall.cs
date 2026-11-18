namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A store call the plan makes when applied (shown before the approval).
/// </summary>
public class PlanCall
{
    /// <summary>
    /// HTTP method.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Path, without host and query.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// What the call changes.
    /// </summary>
    public string Target { get; set; }
}
