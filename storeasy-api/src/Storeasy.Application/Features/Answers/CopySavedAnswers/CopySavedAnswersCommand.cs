using Milvasoft.Components.CQRS.Command;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.CopySavedAnswers;

/// <summary>
/// Copies saved answer documents from another app of the organization; documents of the target are overwritten. The demo account password is never copied. Returns the number of copied documents.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CopySavedAnswersCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App to copy from.
    /// </summary>
    public int SourceAppId { get; set; }

    /// <summary>
    /// App to copy to.
    /// </summary>
    public int TargetAppId { get; set; }

    /// <summary>
    /// Documents to copy; empty copies every one.
    /// </summary>
    public List<SavedAnswerKind> Keys { get; set; } = [];
}
