namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A non blocking remark on a change plan (blocking problems never produce a plan).
/// </summary>
public class PlanWarning
{
    /// <summary>
    /// Message key of the remark.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Text in the language of the request that made the plan.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Field the remark is about, if one.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Locale the remark is about, if one.
    /// </summary>
    public string Locale { get; set; }
}
