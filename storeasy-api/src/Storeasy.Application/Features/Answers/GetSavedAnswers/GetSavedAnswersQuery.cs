using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AnswerDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.GetSavedAnswers;

/// <summary>
/// Reads the saved answer documents of an app. The demo account password is never returned, only whether one is sealed.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSavedAnswersQuery : IQuery<SavedAnswerSetDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }
}
