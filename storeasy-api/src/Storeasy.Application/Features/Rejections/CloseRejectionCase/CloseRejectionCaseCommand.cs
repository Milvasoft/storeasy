using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Rejections.CloseRejectionCase;

/// <summary>
/// Closes a case by hand (the version was withdrawn, for example). A resubmission closes it on its own.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CloseRejectionCaseCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Case.
    /// </summary>
    public int CaseId { get; set; }

    /// <summary>
    /// Why it is closed.
    /// </summary>
    public string Note { get; set; }
}
