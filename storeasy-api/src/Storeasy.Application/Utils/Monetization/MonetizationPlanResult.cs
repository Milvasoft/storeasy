using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// The plan a monetization request makes, or why it cannot be made: a message key and the field it is about (e.g.
/// "Prices[IN].Amount"). Blocking problems never produce a plan; warnings ride on the plan.
/// </summary>
/// <param name="Draft">The plan, when it could be made.</param>
/// <param name="ErrorKey">Message key of the problem.</param>
/// <param name="ErrorDetail">Field or value the problem is about.</param>
public sealed record MonetizationPlanResult(PlanDraft Draft, string ErrorKey = null, string ErrorDetail = null)
{
    /// <summary>
    /// Whether the plan could be made.
    /// </summary>
    public bool IsSuccess => this.ErrorKey == null;
}
