using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Listings.PlanReviewInfo;

/// <summary>
/// Plans the App Review details of the version (PUT review-info). Values not sent come from the saved answers when UseSavedAnswers is set. A demo password is sealed into the app's saved answers first and only the apply job opens it; it is never part of the plan.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanReviewInfoCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Contact first name.
    /// </summary>
    public string ContactFirstName { get; set; }

    /// <summary>
    /// Contact last name.
    /// </summary>
    public string ContactLastName { get; set; }

    /// <summary>
    /// Contact e-mail.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Contact phone.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Whether sign in is required.
    /// </summary>
    public bool? DemoAccountRequired { get; set; }

    /// <summary>
    /// Demo account name.
    /// </summary>
    public string DemoAccountName { get; set; }

    /// <summary>
    /// Demo account password; sealed into the saved answers, never stored in the plan.
    /// </summary>
    [LogMasked]
    public string DemoPassword { get; set; }

    /// <summary>
    /// Notes for the reviewer.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Take the values not sent (and the sealed password) from the saved answers.
    /// </summary>
    public bool UseSavedAnswers { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
