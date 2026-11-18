using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One field level change of a change plan.
/// </summary>
public class PlanItem
{
    /// <summary>
    /// Content group (see Content.ContentGroups).
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Locale; null for values that are not localized.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Field (or class key, territory, track).
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Value in the store when the plan was made; null when there is none. Secrets are masked.
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// Value the plan writes; null to clear. Secrets are masked.
    /// </summary>
    public string After { get; set; }

    /// <summary>
    /// What the item does.
    /// </summary>
    public ChangeAction Action { get; set; }
}
